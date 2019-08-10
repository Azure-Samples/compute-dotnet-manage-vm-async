---
page_type: sample
languages: java
products: azure
services: Compute
platforms: dotnet
author: yaohaizh
---

# Getting Started with Compute - Manage Virtual Machines - in C# asynchronously #

          Azure Compute sample for managing virtual machines -
           - Create a virtual machine with managed OS Disk based on Windows OS image
           - Once Network is created start creation of virtual machine based on Linux OS image in the same network
           - Update both virtual machines
             - for Linux based:
               - add Tag
             - for Windows based:
               - deallocate the virtual machine
               - add a data disk
               - start the virtual machine
           - List virtual machines and print details
           - Delete all virtual machines.


## Running this Sample ##

To run this sample:

Set the environment variable `AZURE_AUTH_LOCATION` with the full path for an auth file. See [how to create an auth file](https://github.com/Azure/azure-libraries-for-net/blob/master/AUTH.md).

    git clone https://github.com/Azure-Samples/compute-dotnet-manage-vm-async.git

    cd compute-dotnet-manage-vm-async
  
    dotnet build
    
    bin\Debug\net452\ManageVirtualMachineAsync.exe

## More information ##

[Azure Management Libraries for C#](https://github.com/Azure/azure-sdk-for-net/tree/Fluent)
[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)
If you don't have a Microsoft Azure subscription you can get a FREE trial account [here](http://go.microsoft.com/fwlink/?LinkId=330212)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.