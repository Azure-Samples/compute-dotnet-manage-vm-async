// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using System.Security.Cryptography.X509Certificates;

namespace ManageVirtualMachineAsync
{
    public class Program
    {
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

        private static ResourceIdentifier? _resourceGroupId = null;

        public static async Task RunSample(ArmClient client)
        {
            var region = AzureLocation.WestCentralUS;
            var windowsVmName = Utilities.CreateRandomName("wVM");
            var linuxVmName = Utilities.CreateRandomName("lVM");
            var rgName = Utilities.CreateRandomName("rgCOMV");
            var subnetName = Utilities.CreateRandomName("sub");
            var vnetName = Utilities.CreateRandomName("vnet");
            var ipConfigName = Utilities.CreateRandomName("config");
            var nicName = Utilities.CreateRandomName("nic");
            var ipConfigName2 = Utilities.CreateRandomName("config");
            var nicName2 = Utilities.CreateRandomName("nic");
            var userName = Utilities.CreateUsername();
            var password = Utilities.CreatePassword();

            try
            {
                //=============================================================
                // Create a Windows virtual machine

                //============================================================
                // Create resource group
                //
                var subscription = await client.GetDefaultSubscriptionAsync();
                var resourceGroupData = new ResourceGroupData(AzureLocation.SouthCentralUS);
                var resourceGroup = (await subscription.GetResourceGroups()
                    .CreateOrUpdateAsync(WaitUntil.Completed, rgName, resourceGroupData)).Value;
                _resourceGroupId = resourceGroup.Id;

                // Create a data disk to attach to VM
                //
                var diskData = new ManagedDiskData(region)
                {
                    DiskSizeGB = 50,
                    CreationData = new DiskCreationData(DiskCreateOption.Empty)
                };
                var dataDisk = (await resourceGroup.GetManagedDisks()
                    .CreateOrUpdateAsync(WaitUntil.Completed, Utilities.CreateRandomName("dsk-"), diskData)).Value;

                Utilities.Log("Creating a Windows VM");

                var t1 = new DateTime();

                // Create related network resource
                //
                var vnetData = new VirtualNetworkData()
                {
                    Location = region,
                    AddressPrefixes = { "10.0.0.0/16" },
                    Subnets = { new SubnetData() { Name = subnetName, AddressPrefix = "10.0.0.0/28" } }
                };
                var vnet = (await resourceGroup.GetVirtualNetworks()
                    .CreateOrUpdateAsync(WaitUntil.Completed, vnetName, vnetData)).Value;
                var subnet = (await vnet.GetSubnets().GetAsync(subnetName)).Value;

                var nicData = new NetworkInterfaceData()
                {
                    Location = region,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName,
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        }
                    }
                };
                var nic = (await resourceGroup.GetNetworkInterfaces()
                    .CreateOrUpdateAsync(WaitUntil.Completed, nicName, nicData)).Value;

                var vmData = new VirtualMachineData(region)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = windowsVmName,
                        AdminUsername = userName,
                        AdminPassword = password
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic.Id,
                                Primary = true,
                            }
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            Name = windowsVmName,
                            OSType = SupportedOperatingSystemType.Windows,
                            Caching = CachingType.ReadWrite,
                            ManagedDisk = new()
                            {
                                StorageAccountType = StorageAccountType.StandardLrs
                            }
                        },
                        DataDisks =
                        {
                            new VirtualMachineDataDisk(1, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 100,
                                ManagedDisk = new()
                                {
                                    StorageAccountType = StorageAccountType.StandardLrs
                                }
                            },
                            new VirtualMachineDataDisk(2, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 10,
                                Caching = CachingType.ReadWrite,
                                ManagedDisk = new()
                                {
                                    StorageAccountType = StorageAccountType.StandardLrs
                                }
                            },
                            new VirtualMachineDataDisk(3, DiskCreateOptionType.Attach)
                            {
                                ManagedDisk = new()
                                {
                                    Id = dataDisk.Id
                                }
                            }
                        },
                        ImageReference = new ImageReference()
                        {
                            Publisher = "MicrosoftWindowsServer",
                            Offer = "WindowsServer",
                            Sku = "2016-Datacenter",
                            Version = "latest",
                        }
                    },
                };

                var windowsVM = (await resourceGroup.GetVirtualMachines()
                    .CreateOrUpdateAsync(WaitUntil.Completed, windowsVmName, vmData)).Value;

                var t2 = new DateTime();
                Utilities.Log($"Created VM: (took {(t2 - t1).TotalSeconds} seconds) " + windowsVM.Id);

                //=============================================================
                // Create a Linux VM in the same virtual network

                Utilities.Log("Creating a Linux VM in the network");

                var nicData2 = new NetworkInterfaceData()
                {
                    Location = region,
                    IPConfigurations = {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = ipConfigName2,
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Primary = false,
                            Subnet = new SubnetData()
                            {
                                Id = subnet.Id
                            }
                        }
                    }
                };
                var nic2 = (await resourceGroup.GetNetworkInterfaces()
                    .CreateOrUpdateAsync(WaitUntil.Completed, nicName2, nicData2)).Value;

                var vmData2 = new VirtualMachineData(region)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        ComputerName = linuxVmName,
                        AdminUsername = userName,
                        AdminPassword = password
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic2.Id,
                                Primary = true,
                            }
                        }
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            Caching = CachingType.ReadWrite,
                            ManagedDisk = new()
                            {
                                StorageAccountType = StorageAccountType.StandardLrs
                            }
                        },
                        ImageReference = new ImageReference()
                        {
                            Publisher = "Canonical",
                            Offer = "UbuntuServer",
                            Sku = "16.04-LTS",
                            Version = "latest",
                        }
                    },
                };

                var linuxVM = (await resourceGroup.GetVirtualMachines()
                    .CreateOrUpdateAsync(WaitUntil.Completed, linuxVmName, vmData2)).Value;

                Utilities.Log("Created a Linux VM (in the same virtual network): " + linuxVM.Id);

                //=============================================================
                // Update - Tag the virtual machine
                var patch = new VirtualMachinePatch()
                {
                    Tags =
                    {
                        {"who-rocks-on-linux", "java" },
                        { "where", "on azure"}
                    }
                };
                linuxVM = (await linuxVM.UpdateAsync(WaitUntil.Completed, patch)).Value;

                Utilities.Log("Tagged Linux VM: " + linuxVM.Id);

                //=============================================================
                // Update - Add a data disk on Windows VM.
                var patch2 = new VirtualMachinePatch()
                {
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        DataDisks =
                        {
                            new VirtualMachineDataDisk(4, DiskCreateOptionType.Empty)
                            {
                                DiskSizeGB = 200,
                                Caching = CachingType.ReadWrite,
                                ManagedDisk = new()
                                {
                                    StorageAccountType = StorageAccountType.StandardLrs
                                }
                            }
                        }
                    }
                };
                windowsVM = (await windowsVM.UpdateAsync(WaitUntil.Completed, patch2)).Value;

                Utilities.Log("Expanded VM " + windowsVM.Id + "'s OS and data disks");
                
                //=============================================================
                // List virtual machines in the resource group

                Utilities.Log("Printing list of VMs =======");

                await foreach (var virtualMachine in resourceGroup.GetVirtualMachines().GetAllAsync())
                {
                    Utilities.Log(virtualMachine.Data.Name);
                }

                //=============================================================
                // Delete the virtual machine
                Utilities.Log("Deleting VM: " + windowsVM.Id);

                await windowsVM.DeleteAsync(WaitUntil.Completed);

                Utilities.Log("Deleted VM: " + windowsVM.Id);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Console.WriteLine($"Deleting Resource Group: {_resourceGroupId}");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Console.WriteLine($"Deleted Resource Group: {_resourceGroupId}");
                    }
                }
                catch (Exception ex)
                {
                    Utilities.Log(ex);
                }
            }
        }

        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var credential = new DefaultAzureCredential();

                var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
                // you can also use `new ArmClient(credential)` here, and the default subscription will be the first subscription in your list of subscription
                var client = new ArmClient(credential, subscriptionId);

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}