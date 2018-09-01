# What is it

A set of plugins that can be used inside the [Neo core library](https://github.com/neo-project/neo).


## How to use

### Basic steps to follow:

* Compile the [neo-cli](https://github.com/neo-project/neo-cli) project as usual.
* Compile the plugin you desire to use and copy its `.dll` to a folder called `Plugins` inside the `neo-cli` compiled release.

### Create your personalized plugin

If you think that your plugin can not fit inside the already create set of Plugins, otherwise you can also create your specific class.

* Firstly, we suggest you to go to [Neo Core Plugins](https://github.com/neo-project/neo/tree/master/neo/Plugins) and add the basic template of your function there;
* After that, find the specific spot of the Neo Blockchain that you want to add a specific functionality.
  * Make sure that the select file contains `using Neo.Plugins;`
  * Insert your plugin functionality after checking if the plugin exists `                foreach (IPolicyPlugin plugin in Plugin.Policies)`. `IPolicyPlugin` is the class that contain the function you want to invoke at that point, `Policies` is a set of plugins (TODO: check the objects that can be inside Policies);
  * For creating a specific class of plugins you should:
    * Edit the main `Plugin center`(https://github.com/neo-project/neo/blob/6f9208d2e96dda7dae3b5bca2e9ecccc69c03531/neo/Plugins/Plugin.cs) with your new class, as well as create a root template folder in [Neo Core Plugins]( https://github.com/neo-project/neo/tree/6f9208d2e96dda7dae3b5bca2e9ecccc69c03531/neo/Plugins)
