# Using plugins
Plugins can be used to increase functionality in neo-cli. One common example is to add the ApplicationLogs plugin in order to enable your node to same log files.

To configure a plugin with neo-cli, do the following:
 - Clone this repository;
 - Open it in Visual Studio, select the plugin you want to enable and select `publish` \(compile it using Release configuration\)
 - Create the Plugins folder in neo-cli (where the binary is run from, like `/neo-cli/bin/debug/netcoreapp2.1/Plugins`)
 - Copy the .dll and the folder with the configuration files into this Plugin folder.
 - Start neo-cli using additional parameters, if required;
  - In order to start logging, start neo-cli with the  `--log` option.

The resulting structure is going to be something like this (example):
`./neo-cli.dll
./Plugins/ApplicationLogs.dll
./Plugins/ApplicationsLogs/config.json`
