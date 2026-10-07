# WSL / Linux CLI build

Ubuntu on WSL can run the OS-independent tests and build the Linux IBus package.

```bash
cd ~/path/to/Meltype
chmod +x linux/build-cli.sh
linux/build-cli.sh --setup --all
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
