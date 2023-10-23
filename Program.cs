// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Net;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute;
using System.Net.NetworkInformation;
using System.Xml.Linq;

namespace ManageVirtualMachineAsync
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
       
        /**
         * Azure Compute sample for managing virtual machines -
         *  - Create a virtual machine with managed OS Disk based on Windows OS image
         *  - Once Network is created start creation of virtual machine based on Linux OS image in the same network
         *  - Update both virtual machines
         *    - for Linux based:
         *      - add Tag
         *    - for Windows based:
         *      - deallocate the virtual machine
         *      - add a data disk
         *      - start the virtual machine
         *  - List virtual machines and print details
         *  - Delete all virtual machines.
         */
        public static async Task RunSample(ArmClient client)
        {
            var windowsVmName = Utilities.CreateRandomName("wVM");
            var linuxVmName = Utilities.CreateRandomName("lVM");
            var rgName = Utilities.CreateRandomName("rgCOMV");
            var userName = Utilities.CreateUsername();
            var password = Utilities.CreatePassword();

            try
            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                Utilities.Log($"creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //=============================================================
                // Create a Windows virtual machine

                // Create two data disk to attach to VM
                //
                var dataDiskCreatable = azure.Disks.Define(Utilities.CreateRandomName("dsk-"))
                        .WithRegion(region)
                        .WithExistingResourceGroup(rgName)
                        .WithData()
                        .WithSizeInGB(100);

                //
                var dataDisk = await azure.Disks.Define(Utilities.CreateRandomName("dsk-"))
                        .WithRegion(region)
                        .WithNewResourceGroup(rgName)
                        .WithData()
                        .WithSizeInGB(50)
                        .CreateAsync();

                Utilities.Log("Creating a Windows VM");

                var t1 = new DateTime();

                var windowsVM = await azure.VirtualMachines.Define(windowsVmName)
                        .WithRegion(region)
                        .WithNewResourceGroup(rgName)
                        .WithNewPrimaryNetwork("10.0.0.0/28")
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithoutPrimaryPublicIPAddress()
                        .WithPopularWindowsImage(KnownWindowsVirtualMachineImage.WindowsServer2012R2Datacenter)
                        .WithAdminUsername(userName)
                        .WithAdminPassword(password)
                        .WithNewDataDisk(10)
                        .WithNewDataDisk(dataDiskCreatable)
                        .WithExistingDataDisk(dataDisk)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                        .CreateAsync();

                var t2 = new DateTime();
                Utilities.Log($"Created VM: (took {(t2 - t1).TotalSeconds} seconds) " + windowsVM.Id);
                // Print virtual machine details
                Utilities.PrintVirtualMachine(windowsVM);

                // Get the network where Windows VM is hosted
                var network = windowsVM.GetPrimaryNetworkInterface().PrimaryIPConfiguration.GetNetwork();

                //=============================================================
                // Create a Linux VM in the same virtual network

                Utilities.Log("Creating a Linux VM in the network");

                var linuxVM = await azure.VirtualMachines.Define(linuxVmName)
                        .WithRegion(region)
                        .WithExistingResourceGroup(rgName)
                        .WithExistingPrimaryNetwork(network)
                        .WithSubnet("subnet1") // Referencing the default subnet name when no name specified at creation
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithoutPrimaryPublicIPAddress()
                        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                        .WithRootUsername(userName)
                        .WithRootPassword(password)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                        .CreateAsync();

                Utilities.Log("Created a Linux VM (in the same virtual network): " + linuxVM.Id);
                Utilities.PrintVirtualMachine(linuxVM);

                //=============================================================
                // Update - Tag the virtual machine

                await linuxVM.Update()
                        .WithTag("who-rocks-on-linux", "java")
                        .WithTag("where", "on azure")
                        .ApplyAsync();

                Utilities.Log("Tagged Linux VM: " + linuxVM.Id);

                //=============================================================
                // Update - Add a data disk on Windows VM.

                await windowsVM.Update()
                        .WithNewDataDisk(200)
                        .ApplyAsync();

                Utilities.Log("Expanded VM " + windowsVM.Id + "'s OS and data disks");
                Utilities.PrintVirtualMachine(windowsVM);
                
                //=============================================================
                // List virtual machines in the resource group

                var resourceGroupName = windowsVM.ResourceGroupName;

                Utilities.Log("Printing list of VMs =======");

                foreach (var virtualMachine in await azure.VirtualMachines.ListByResourceGroupAsync(resourceGroupName))
                {
                    Utilities.PrintVirtualMachine(virtualMachine);
                }

                //=============================================================
                // Delete the virtual machine
                Utilities.Log("Deleting VM: " + windowsVM.Id);

                await azure.VirtualMachines.DeleteByIdAsync(windowsVM.Id);

                Utilities.Log("Deleted VM: " + windowsVM.Id);
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
                }
                catch (Exception g)
                {
                    Utilities.Log(g);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}