// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Network.Models;

namespace ManageVirtualNetworkAsync
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;

        /**
        * Azure Network sample for managing virtual networks.
        *  - Create a virtual network with Subnets
        *  - Update a virtual network
        *  - Create virtual machines in the virtual network subnets
        *  - Create another virtual network
        *  - List virtual networks
        */
        public async static Task RunSampleAsync(ArmClient client)
        {
            string vnetName1 = Utilities.CreateRandomName("vnet1");
            string vnetName2 = Utilities.CreateRandomName("vnet2");
            string frontEndVmName = Utilities.CreateRandomName("fevm");
            string backEndVmName = Utilities.CreateRandomName("bevm");
            string publicIpAddressLeafDnsForFrontEndVm = Utilities.CreateRandomName("pip1");

            try
            {
                // Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                // Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("NetworkSampleRG");
                Utilities.Log($"Creating resource group...");
                ArmOperation<ResourceGroupResource> rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log("Created a resource group with name: " + resourceGroup.Data.Name);

                //============================================================
                // Create a virtual network with specific address-space and two subnet

                // Creates a network security group for backend subnet

                // Create NSG for backend
                Utilities.Log("Creating a network security group for virtual network backend subnet...");
                string backendNsgName = Utilities.CreateRandomName("backEndNSG");
                NetworkSecurityGroupData backendNsgInput = new NetworkSecurityGroupData()
                {
                    Location = resourceGroup.Data.Location,
                    SecurityRules =
                    {
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetInComing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "INTERNET",
                            DestinationAddressPrefix = "*",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 100,
                            Direction = SecurityRuleDirection.Inbound,
                        },
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetOutGoing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "*",
                            DestinationAddressPrefix = "internet",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 200,
                            Direction = SecurityRuleDirection.Outbound,
                        }
                    }

                };
                var backendNsgLro = await resourceGroup.GetNetworkSecurityGroups().CreateOrUpdateAsync(WaitUntil.Completed, backendNsgName, backendNsgInput);
                NetworkSecurityGroupResource backendNsg = backendNsgLro.Value;
                Utilities.Log($"Created network security group: {backendNsg.Data.Name}");

                // Create NSG for frontend
                Utilities.Log("Creating a network security group for virtual network frontend subnet...");
                string frontendNsgName = Utilities.CreateRandomName("frontEndNSG");
                NetworkSecurityGroupData frontendNsgInput = new NetworkSecurityGroupData()
                {
                    Location = resourceGroup.Data.Location,
                    SecurityRules =
                    {
                        new SecurityRuleData()
                        {
                            Name = "AllowHttpInComing",
                            Protocol = SecurityRuleProtocol.Tcp,
                            SourcePortRange = "*",
                            DestinationPortRange = "80",
                            SourceAddressPrefix = "INTERNET",
                            DestinationAddressPrefix = "*",
                            Access = SecurityRuleAccess.Allow,
                            Priority = 100,
                            Direction = SecurityRuleDirection.Inbound,
                        },
                        new SecurityRuleData()
                        {
                            Name = "DenyInternetOutGoing",
                            Protocol = SecurityRuleProtocol.Asterisk,
                            SourcePortRange = "*",
                            DestinationPortRange = "*",
                            SourceAddressPrefix = "*",
                            DestinationAddressPrefix = "internet",
                            Access = SecurityRuleAccess.Deny,
                            Priority = 200,
                            Direction = SecurityRuleDirection.Outbound,
                        }
                    }
                };
                var frontendNsgLro = await resourceGroup.GetNetworkSecurityGroups().CreateOrUpdateAsync(WaitUntil.Completed, frontendNsgName, frontendNsgInput);
                NetworkSecurityGroupResource frontendNsg = frontendNsgLro.Value;
                Utilities.Log($"Created network security group: {frontendNsg.Data.Name}");

                // Create virtual network
                Utilities.Log("Creating virtual network #1...");

                string frontendSubnetName = Utilities.CreateRandomName("fesubnet");
                string backendSubnetName = Utilities.CreateRandomName("besubnet");
                VirtualNetworkData vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = frontendSubnetName },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = backendSubnetName, NetworkSecurityGroup = backendNsg.Data }
                    },
                };
                var vnetLro1 = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName1, vnetInput1);
                VirtualNetworkResource vnet1 = vnetLro1.Value;
                Utilities.Log($"Created a virtual network: {vnet1.Data.Name}");

                //============================================================
                // Update a virtual network

                // Update the virtual network frontend subnet by associating it with network security group

                Utilities.Log("Associating network security group rule to frontend subnet");

                vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = frontendSubnetName,  NetworkSecurityGroup = frontendNsg.Data },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = backendSubnetName, NetworkSecurityGroup = backendNsg.Data }
                    },
                };
                vnetLro1 = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName1, vnetInput1);
                vnet1 = vnetLro1.Value;
                Utilities.Log("Network security group rule associated with the frontend subnet");

                //============================================================
                // Create a virtual machine in each subnet

                // Creates the first virtual machine in frontend subnet
                Utilities.Log("Creating a virtual machine in the frontend subnet");
                SubnetData feSubnet = vnet1.Data.Subnets.First(item => item.Name == frontendSubnetName);
                var frontEndVM = await Utilities.CreateVirtualMachine(resourceGroup, feSubnet.Id, frontEndVmName);
                Utilities.Log($"Created VM: {frontEndVM.Data.Name}");

                // Creates the second virtual machine in the backend subnet
                Utilities.Log("Creating a virtual machine in the backend subnet");
                SubnetData beSubnet = vnet1.Data.Subnets.First(item => item.Name == backendSubnetName);
                var backEndVM = await Utilities.CreateVirtualMachine(resourceGroup, beSubnet.Id, backEndVmName);
                Utilities.Log($"Created VM: {backEndVM.Data.Name}");

                // Create a virtual network with default address-space and one default subnet
                Utilities.Log("Creating virtual network #2...");

                VirtualNetworkData vnetInput2 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "10.10.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "10.10.1.0/24", Name = "default" },
                    },
                };
                var vnetLro2 = await resourceGroup.GetVirtualNetworks().CreateOrUpdateAsync(WaitUntil.Completed, vnetName2, vnetInput2);
                var vnet2 = vnetLro2.Value;
                Utilities.Log($"Created a virtual network: {vnet2.Data.Name}");

                //============================================================
                // List virtual networks


                Utilities.Log($"Get all virtual network under {resourceGroup.Data.Name}");
                await foreach (var virtualNetwork in resourceGroup.GetVirtualNetworks().GetAllAsync())
                {
                    Utilities.Log("\t" + virtualNetwork.Data.Name);
                }

                //============================================================
                // Delete a virtual network
                Utilities.Log("Deleting the virtual network");
                await vnet2.DeleteAsync(WaitUntil.Completed);
                Utilities.Log("Deleted the virtual network");
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }

                catch (NullReferenceException)
                {
                    Utilities.Log("Did not create any resources in Azure. No clean up is necessary");
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
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSampleAsync(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}