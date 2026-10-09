# WSL / Linux CLI build

Ubuntu on WSL can run the OS-independent tests and build the Linux IBus package.
Run these commands in the Ubuntu/WSL terminal, not in PowerShell or macOS.
The output is a Linux package; it does not install a Windows-wide or Mac input method.

```bash
cd ~/path/to/Meltype
bash linux/build-cli.sh --setup --all
```

The default `--all` flow runs the core tests, builds the Mozc helper, then creates:

```text
linux/build/Meltype-linux/
```

Install the built IBus engine inside the Linux environment:

```bash
linux/build-cli.sh --install
```

For day-to-day work, these narrower commands are useful:

```bash
linux/build-cli.sh --test
bash linux/build-cli.sh --build
linux/build-cli.sh --package
linux/build-cli.sh --mozc --package
```

After installing, restart the Linux session if Meltype does not appear in the
input source list. For debugging the engine:

```bash
ibus restart
sleep 2
pkill -f ibus-engine-meltype || true
/opt/meltype/ibus-engine-meltype
```
