## What is it
A set of plugins that can be used inside the Neo core library. Check [here](http://docs.neo.org/en-us/node/plugin.html) for the official documentation.

## Using plugins
Plugins can be used to increase functionality in neo. One common example is to add the ApplicationLogs plugin in order to enable your node to create log files.

To configure a plugin, do the following:
 - Download the desired plugin from the [Releases page](https://github.com/neo-project/neo-plugins/releases)
  - Alternative: Compile from source
    - Clone this repository;
    - Open it in Visual Studio, select the plugin you want to enable and select `publish` \(compile it using Release configuration\)
    - Create the Plugins folder in neo-cli / neo-gui (where the binary is run from, like `/neo-cli/bin/debug/netcoreapp2.1/Plugins`)
 - Copy the .dll and the folder with the configuration files into this Plugin folder.
 - Start neo using additional parameters, if required;
 	- In order to start logging, start neo with the `--log` option.

The resulting folder structure is going to be like this:

```BASH
./neo-cli.dll
./Plugins/ApplicationLogs.dll
./Plugins/ApplicationsLogs/config.json
```

## Existing plugins
### Application Logs
Add this plugin to your application if need to access the log files. This can be useful to handle notifications, but remember that this also largely increases the space used by the application.

### Import Blocks
Synchronizes the client using offline packages. Follow the instructions [here](http://docs.neo.org/en-us/network/syncblocks.html) to bootstrap your network using this plugin.

### RPC Security
Improves security in RPC nodes.

### Simple Policy
Enables policies for consensus.

### StatesDumper
Exports NEO-CLI status data \(useful for debugging\).
