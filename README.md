## What is it
A set of plugins that can be used inside the NEO core library are available in this repository. You can refer to [the official documentation](https://docs.neo.org/docs/en-us/node/cli/setup.html) for the more detailed usage guide. In addition, a C# SDK is included for developers to call RPC methods with ease.

## Using Plugins
Plugins can be used to increase functionality, as well as providing policies definitions of the network.
One common example is to add the ApplicationLogs plugin in order to enable your node to create log files.

To configure a plugin, you can directly download the desired plugin from the [Releases page](https://github.com/neo-project/neo-modules/releases)。

Alternatively, you can compile from source code by following the below steps:
- Clone this repository;
- Open it in Visual Studio, select the plugin you want to enable and select `publish` \(compile it using Release configuration\)
- Create the Plugins folder in neo-cli / neo-gui (where the binary file is located, such as `/neo-cli/bin/Release/netcoreapp3.0/Plugins`)
- Copy the .dll and the folder with the configuration files into the `Plugins` folder.
  - Remarkably, you should put the dependency of the plugin in the `Plugins` folder as well. For example, since the `RpcServer` has the package reference on the `Microsoft.AspNetCore.ResponseCompression`, so the corresponding dll file should be put together with the plugin.
- Start neo using additional parameters, if required;
  - In order to start logging, start neo with the `--log` option.

The resulting folder structure is going to be like this:

```sh
./neo-cli.dll
./Plugins/ApplicationLogs.dll
./Plugins/ApplicationsLogs/config.json
```

## Plugins
### LevelDB Storage Engine
If there is no further modification of the configuration file of the neo-node, it is the default storage engine in the NEO system. In this case, you should paste the `LevelDBStore` in the Plugins before launching the node.

### RocksDB Storage Engine
It is the choice of users for the storage engine. You can also use `RocksDBStore` in the NEO system by modifying the default storage engine section in the configuration file.

### RPC Server
Currently, RPC server has been decoupled with the NEO library. You can install this plugin to provide RPC service outside. Specifically, it is required to open the wallet for calling some RPC methods. For more details, you can refer to [RPC APIs](https://docs.neo.org/docs/zh-cn/reference/rpc/latest-version/api.html).  

### RPC NEP5 Tracker
This plugin can help you get the NEP-5 transaction information for the specified address. You should install the plugin `RpcServer` before enabling `RpcNep5Tracker`. [Here](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getnep5transfers.html) is the use case for this plugin.

### StatesDumper
Exports NEO-CLI status data \(useful for debugging\).

### SystemLog
Enable neo-cli Logging with timestamps by showing messages with different levels (shown with different colors) \(useful for debugging\).

### Application Logs
Add this plugin to your application if need to access the log files. This can be useful to handle notifications, but remember that this also largely increases the space used by the application. `RpcServer` is also needed for this plugin. You can find more details [here](https://docs.neo.org/docs/en-us/reference/rpc/latest-version/api/getapplicationlog.html).

## C# SDK
### RPC Client
The RPC client to call NEO RPC methods.
