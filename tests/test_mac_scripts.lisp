;;; Run with: sbcl --script tests/test_mac_scripts.lisp
;;; Exercise routing with fake tools; never touch the installed input method.
(require :asdf)

(defparameter *root*
  (uiop:pathname-parent-directory-pathname
   (uiop:pathname-directory-pathname *load-truename*)))

(defun write-file (path text)
  (ensure-directories-exist path)
  (with-open-file (stream path :direction :output :if-exists :supersede)
    (write-string text stream)))

(defun fake-tool (work name body)
  (let ((path (merge-pathnames name work)))
    (write-file path (format nil "#!/bin/bash~%~A~%" body))
    (uiop:run-program (list "chmod" "+x" (namestring path)))))

(defun run-script (work name argument)
  (uiop:run-program
   (append (list "env"
                 (concatenate 'string "PATH=" (namestring work) ":" (uiop:getenv "PATH"))
                 (concatenate 'string "MELTYPE_TEST_LOG="
                              (namestring (merge-pathnames "calls" work)))
                 "bash" (namestring (merge-pathnames name work)))
           (when argument (list argument)))
   :output *standard-output* :error-output *error-output*))

(defun call-with-work-directory (test)
  (let ((work (uiop:ensure-directory-pathname
               (string-trim '(#\Newline #\Return)
                            (uiop:run-program
                             '("mktemp" "-d" "/tmp/meltype-lisp-tests.XXXXXX")
                             :output :string)))))
    (unwind-protect (funcall test work)
      (uiop:delete-directory-tree work :validate t))))

(defun test-build-only (work)
  (write-file (merge-pathnames "build-cli.sh" work)
              (uiop:read-file-string (merge-pathnames "mac/build-cli.sh" *root*)))
  (fake-tool work "build.sh" "echo \"$*\" >> \"$MELTYPE_TEST_LOG\"")
  (dolist (name '("dotnet" "swift")) (fake-tool work name "exit 0"))
  (run-script work "build-cli.sh" "--build")
  (assert (equal '("--no-install")
                 (uiop:read-file-lines (merge-pathnames "calls" work)))))

(defun test-update (work)
  (let* ((source (uiop:read-file-string (merge-pathnames "mac/update.sh" *root*)))
         (original "$HOME/Library/Input Methods/Meltype.app")
         (position (search original source))
         (app (namestring (merge-pathnames "Meltype.app" work))))
    (assert position)
    (write-file (merge-pathnames "update.sh" work)
                (concatenate 'string (subseq source 0 position) app
                             (subseq source (+ position (length original))))))
  (fake-tool work "Meltype.app/Contents/MacOS/Meltype"
             "echo \"app:$*\" >> \"$MELTYPE_TEST_LOG\"")
  (fake-tool work "build-cli.sh"
             "echo \"build:$MELTYPE_SKIP_START:$*\" >> \"$MELTYPE_TEST_LOG\"")
  (fake-tool work "uname" "echo Darwin")
  (fake-tool work "swift" "echo select >> \"$MELTYPE_TEST_LOG\"")
  (fake-tool work "open" "echo UNEXPECTED_OPEN >> \"$MELTYPE_TEST_LOG\"; exit 1")
  (run-script work "update.sh" nil)
  (let ((lines (uiop:read-file-lines (merge-pathnames "calls" work))))
    (assert (equal "build:1:--test --install" (first lines)))
    (assert (uiop:string-prefix-p "app:--check-inputs " (second lines)))
    (dolist (expected '("app:" "app:--register-input-source" "select"))
      (assert (member expected lines :test #'equal)))
    (assert (not (member "UNEXPECTED_OPEN" lines :test #'equal)))))

(handler-case
    (progn
      (call-with-work-directory #'test-build-only)
      (format t "PASS build-only routing~%")
      (call-with-work-directory #'test-update)
      (format t "PASS update routing~%2/2 passed~%"))
  (error (condition)
    (format *error-output* "FAIL: ~A~%" condition)
    (uiop:quit 1)))
