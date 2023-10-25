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
            string rgName = Utilities.CreateRandomName("ComputeSampleRG");
            string vnetName = Utilities.CreateRandomName("vnet");
            string nicName1 = Utilities.CreateRandomName("nic1-");
            string nicName2 = Utilities.CreateRandomName("nic2-");
            string diskName1 = Utilities.CreateRandomName("disk1-");
            string diskName2 = Utilities.CreateRandomName("disk2-");
            string diskName3 = Utilities.CreateRandomName("disk3-");
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
                Utilities.Log("Creating two empty managed disk");
                ManagedDiskData diskInput1 = new ManagedDiskData(resourceGroup.Data.Location)
                {
                    Sku = new DiskSku()
                    {
                        Name = DiskStorageAccountType.StandardLrs
                    },
                    CreationData = new DiskCreationData(DiskCreateOption.Empty),
                    DiskSizeGB = 100,
                };
                var diskLro1 = await resourceGroup.GetManagedDisks().CreateOrUpdateAsync(WaitUntil.Completed, diskName1, diskInput1);
                ManagedDiskResource disk1 = diskLro1.Value;
                Utilities.Log($"Created managed disk: {disk1.Data.Name}");

                ManagedDiskData diskInput2 = new ManagedDiskData(resourceGroup.Data.Location)
                {
                    Sku = new DiskSku()
                    {
                        Name = DiskStorageAccountType.StandardLrs
                    },
                    CreationData = new DiskCreationData(DiskCreateOption.Empty),
                    DiskSizeGB = 50,
                };
                var diskLro2 = await resourceGroup.GetManagedDisks().CreateOrUpdateAsync(WaitUntil.Completed, diskName2, diskInput2);
                ManagedDiskResource disk2 = diskLro2.Value;
                Utilities.Log($"Created managed disk: {disk2.Data.Name}");

                // Pre-creating some resources that the VM depends on
                Utilities.Log("Pre-creating some resources that the VM depends on");

                // Creating a virtual network
                var vnet = await Utilities.CreateVirtualNetwork(resourceGroup, vnetName);

                // Creating network interface
                var nic1 = await Utilities.CreateNetworkInterface(resourceGroup, vnet.Data.Subnets[0].Id, nicName1);
                var nic2 = await Utilities.CreateNetworkInterface(resourceGroup, vnet.Data.Subnets[1].Id, nicName2);

                Utilities.Log();
                Utilities.Log("Creating a Windows VM");
                var t1 = new DateTime();

                VirtualMachineData windowsVMInput = new VirtualMachineData(resourceGroup.Data.Location)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardDS1V2
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        ImageReference = new ImageReference()
                        {
                            Publisher = "MicrosoftWindowsDesktop",
                            Offer = "Windows-10",
                            Sku = "win10-21h2-ent",
                            Version = "latest",
                        },
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            OSType = SupportedOperatingSystemType.Windows,
                            Name = "windowsVMOSDisk",
                            Caching = CachingType.ReadOnly,
                            ManagedDisk = new VirtualMachineManagedDisk()
                            {
                                StorageAccountType = StorageAccountType.StandardLrs,
                            },
                        },
                        DataDisks =
                        {
                            new VirtualMachineDataDisk(1, DiskCreateOptionType.Attach)
                            {
                                Name = disk1.Data.Name,
                                Caching = CachingType.ReadOnly,
                                DiskSizeGB = 128,
                                ManagedDisk = new VirtualMachineManagedDisk()
                                {
                                    Id = disk1.Id,
                                }
                            },
                            new VirtualMachineDataDisk(2, DiskCreateOptionType.Attach)
                            {
                                Name = disk2.Data.Name,
                                Caching = CachingType.ReadOnly,
                                DiskSizeGB = 256,
                                ManagedDisk = new VirtualMachineManagedDisk()
                                {
                                    Id = disk2.Id,
                                }
                            },
                        }
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        AdminUsername = Utilities.CreateUsername(),
                        AdminPassword = Utilities.CreatePassword(),
                        ComputerName = windowsVmName,
                    },
                    NetworkProfile = new VirtualMachineNetworkProfile()
                    {
                        NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = nic1.Id,
                                Primary = true,
                            }
                        }
                    },
                };
                var windowsVMLro = await resourceGroup.GetVirtualMachines().CreateOrUpdateAsync(WaitUntil.Completed, windowsVmName, windowsVMInput);
                VirtualMachineResource windowsVM = windowsVMLro.Value;

                var t2 = new DateTime();
                Utilities.Log($"Created VM: (took {(t2 - t1).TotalSeconds} seconds) " + windowsVM.Id.Name);
                Utilities.PrintVirtualMachine(windowsVM);

                //=============================================================
                // Create a Linux VM in the same virtual network

                Utilities.Log("Creating a Linux VM in the network");

                VirtualMachineData linuxVmInput = new VirtualMachineData(resourceGroup.Data.Location)
                {
                    HardwareProfile = new VirtualMachineHardwareProfile()
                    {
                        VmSize = VirtualMachineSizeType.StandardF2
                    },
                    StorageProfile = new VirtualMachineStorageProfile()
                    {
                        ImageReference = new ImageReference()
                        {
                            Publisher = "Canonical",
                            Offer = "UbuntuServer",
                            Sku = "16.04-LTS",
                            Version = "latest",
                        },
                        OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                        {
                            Name  = "LinuxOSDisk",
                            OSType = SupportedOperatingSystemType.Linux,
                            Caching = CachingType.ReadWrite,
                            ManagedDisk = new VirtualMachineManagedDisk()
                            {
                                StorageAccountType = StorageAccountType.StandardLrs
                            }
                        },
                    },
                    OSProfile = new VirtualMachineOSProfile()
                    {
                        AdminUsername = Utilities.CreateUsername(),
                        AdminPassword = Utilities.CreatePassword(),
                        ComputerName = linuxVmName,
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
                };
                var linuxVMLro = await resourceGroup.GetVirtualMachines().CreateOrUpdateAsync(WaitUntil.Completed, linuxVmName, linuxVmInput);
                VirtualMachineResource linuxVM = linuxVMLro.Value;

                Utilities.Log("Created a Linux VM (in the same virtual network): " + linuxVM.Id.Name);
                Utilities.PrintVirtualMachine(linuxVM);

                //=============================================================
                // Update - Tag the virtual machine

                await linuxVM.AddTagAsync("who-rocks-on-linux", "java");
                await linuxVM.AddTagAsync("where", "on azure");

                Utilities.Log("Tagged Linux VM: " + linuxVM.Id.Name);

                //=============================================================
                // Update - Add a data disk on Windows VM.

                // Create a new disk
                ManagedDiskData diskInput3 = new ManagedDiskData(resourceGroup.Data.Location)
                {
                    Sku = new DiskSku()
                    {
                        Name = DiskStorageAccountType.StandardLrs
                    },
                    CreationData = new DiskCreationData(DiskCreateOption.Empty),
                    DiskSizeGB = 50,
                };
                var diskLro3 = await resourceGroup.GetManagedDisks().CreateOrUpdateAsync(WaitUntil.Completed, diskName3, diskInput3);
                ManagedDiskResource disk3 = diskLro3.Value;
                Utilities.Log($"Created managed disk: {disk3.Data.Name}");

                // attach to windows vm
                VirtualMachineData updateInput = windowsVM.Data;
                updateInput.StorageProfile.DataDisks.Add(new VirtualMachineDataDisk(3, DiskCreateOptionType.Attach)
                {
                    Name = disk3.Data.Name,
                    Caching = CachingType.ReadOnly,
                    DiskSizeGB = 200,
                    ManagedDisk = new VirtualMachineManagedDisk()
                    {
                        Id = disk3.Id,
                    }
                });
                windowsVMLro = await resourceGroup.GetVirtualMachines().CreateOrUpdateAsync(WaitUntil.Completed, windowsVmName, updateInput);
                windowsVM = windowsVMLro.Value;

                Utilities.Log("Expanded VM " + windowsVM.Id.Name + "'s OS and data disks");

                //=============================================================
                // List virtual machines in the resource group

                Utilities.Log("Printing list of VMs =======");

                await foreach (var virtualMachine in resourceGroup.GetVirtualMachines().GetAllAsync())
                {
                    Utilities.PrintVirtualMachine(virtualMachine);
                }

                //=============================================================
                // Delete the virtual machine
                Utilities.Log("Deleting VM: " + windowsVM.Id);

                await windowsVM.DeleteAsync(WaitUntil.Completed);

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