﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.ResourceManager.Compute;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Azure.ResourceManager.Samples.Common
{
    public static class Utilities
    {
        public static Action<string> LoggerMethod { get; set; }
        public static Func<string> PauseMethod { get; set; }
        public static string ProjectPath { get; set; }
        private static Random _random => new Random();

        static Utilities()
        {
            LoggerMethod = Console.WriteLine;
            PauseMethod = Console.ReadLine;
            ProjectPath = ".";
        }

        public static void Log(string message)
        {
            LoggerMethod.Invoke(message);
        }

        public static void Log(object obj)
        {
            if (obj != null)
            {
                LoggerMethod.Invoke(obj.ToString());
            }
            else
            {
                LoggerMethod.Invoke("(null)");
            }
        }

        public static void Log()
        {
            Utilities.Log("");
        }

        public static string ReadLine() => PauseMethod.Invoke();

        public static string CreateRandomName(string namePrefix) => $"{namePrefix}{_random.Next(9999)}";

        public static string CreatePassword() => "azure12345QWE!";

        public static string CreateUsername() => "tirekicker";

        public static async Task<VirtualMachineResource> CreateVirtualMachine(ResourceGroupResource resourceGroup, ResourceIdentifier subnetId, string vmName = null)
        {
            vmName = vmName is null ? Utilities.CreateRandomName("vm") : vmName;
            string nicName = Utilities.CreateRandomName("nic");

            var nicInput = new NetworkInterfaceData()
            {
                Location = resourceGroup.Data.Location,
                IPConfigurations =
                {
                    new NetworkInterfaceIPConfigurationData()
                    {
                        Name = "default-config",
                        PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                        Subnet = new SubnetData()
                        {
                            Id = subnetId
                        }
                    }
                }
            };
            var networkInterfaceLro = await resourceGroup.GetNetworkInterfaces().CreateOrUpdateAsync(WaitUntil.Completed, nicName, nicInput);
            var nicId = networkInterfaceLro.Value.Data.Id;

            VirtualMachineCollection vmCollection = resourceGroup.GetVirtualMachines();
            VirtualMachineData vmInput = new VirtualMachineData(resourceGroup.Data.Location)
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
                        Name = CreateRandomName("myVMOSdisk"),
                        Caching = CachingType.ReadOnly,
                        ManagedDisk = new VirtualMachineManagedDisk()
                        {
                            StorageAccountType = StorageAccountType.StandardLrs,
                        },
                    },
                },
                OSProfile = new VirtualMachineOSProfile()
                {
                    AdminUsername = CreateUsername(),
                    AdminPassword = CreatePassword(),
                    ComputerName = vmName,
                },
                NetworkProfile = new VirtualMachineNetworkProfile()
                {
                    NetworkInterfaces =
                    {
                        new VirtualMachineNetworkInterfaceReference()
                        {
                            Id = nicId,
                            Primary = true,
                        }
                    }
                },
            };
            var vmLro = await vmCollection.CreateOrUpdateAsync(WaitUntil.Completed, vmName, vmInput);
            return vmLro.Value;
        }
    }
}
