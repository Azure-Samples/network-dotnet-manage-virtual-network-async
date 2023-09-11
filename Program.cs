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

                Utilities.Log("Creating a network security group for virtual network backend subnet...");
                Utilities.Log("Creating a network security group for virtual network frontend subnet...");

                // Create NSG for backend
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

                string backendSubnetName = Utilities.CreateRandomName("besubnet");
                VirtualNetworkData vnetInput1 = new VirtualNetworkData()
                {
                    Location = resourceGroup.Data.Location,
                    AddressPrefixes = { "192.168.0.0/16" },
                    Subnets =
                    {
                        new SubnetData() { AddressPrefix = "192.168.1.0/24", Name = frontendNsgName },
                        new SubnetData() { AddressPrefix = "192.168.2.0/24", Name = backendSubnetName, NetworkSecurityGroup = backendNsg.Data }
                    },
                };
                var vnetLro1 = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName1, vnetInput1);
                VirtualNetworkResource vnet1 = vnetLro1.Value;
                Utilities.Log($"Created a virtual network: {vnet1.Data.Name}");

                //============================================================
                // Update a virtual network

                // Update the virtual network frontend subnet by associating it with network security group

                Utilities.Log("Associating network security group rule to frontend subnet");

                await virtualNetwork1.Update()
                        .UpdateSubnet(VNet1FrontEndSubnetName)
                            .WithExistingNetworkSecurityGroup(frontEndSubnetNsg)
                            .Parent()
                        .ApplyAsync();

                Utilities.Log("Network security group rule associated with the frontend subnet");
                // Print the virtual network details
                Utilities.PrintVirtualNetwork(virtualNetwork1);

                //============================================================
                // Create a virtual machine in each subnet

                // Creates the first virtual machine in frontend subnet
                Utilities.Log("Creating a Linux virtual machine in the frontend subnet");
                // Creates the second virtual machine in the backend subnet
                Utilities.Log("Creating a Linux virtual machine in the backend subnet");
                // Create a virtual network with default address-space and one default subnet
                Utilities.Log("Creating virtual network #2...");

                var t1 = DateTime.UtcNow;

                var frontEndVM = await azure.VirtualMachines.Define(frontEndVmName)
                        .WithRegion(Region.USEast)
                        .WithExistingResourceGroup(ResourceGroupName)
                        .WithExistingPrimaryNetwork(virtualNetwork1)
                        .WithSubnet(VNet1FrontEndSubnetName)
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithNewPrimaryPublicIPAddress(publicIpAddressLeafDnsForFrontEndVm)
                        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                        .WithRootUsername(UserName)
                        .WithSsh(SshKey)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                        .CreateAsync();
                var t2 = DateTime.UtcNow;
                Utilities.Log("Created Linux VM: (took "
                    + (t2 - t1).TotalSeconds + " seconds) " + frontEndVM);
                // Print virtual machine details
                Utilities.PrintVirtualMachine(frontEndVM);
                t1 = DateTime.UtcNow;

                var backEndVM = await azure.VirtualMachines.Define(backEndVmName)
                        .WithRegion(Region.USEast)
                        .WithExistingResourceGroup(ResourceGroupName)
                        .WithExistingPrimaryNetwork(virtualNetwork1)
                        .WithSubnet(VNet1BackEndSubnetName)
                        .WithPrimaryPrivateIPAddressDynamic()
                        .WithoutPrimaryPublicIPAddress()
                        .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                        .WithRootUsername(UserName)
                        .WithSsh(SshKey)
                        .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                        .CreateAsync();

                var t3 = DateTime.UtcNow;
                Utilities.Log("Created Linux VM: (took "
                        + (t3 - t1).TotalSeconds + " seconds) " + backEndVM.Id);
                // Print virtual machine details
                Utilities.PrintVirtualMachine(backEndVM);

                var virtualNetwork2 = await azure.Networks.Define(vnetName2)
                        .WithRegion(Region.USEast)
                        .WithNewResourceGroup(ResourceGroupName)
                        .CreateAsync();

                Utilities.Log("Created a virtual network");
                // Print the virtual network details
                Utilities.PrintVirtualNetwork(virtualNetwork2);

                //============================================================
                // List virtual networks

                foreach (var virtualNetwork in await azure.Networks.ListByResourceGroupAsync(ResourceGroupName))
                {
                    Utilities.PrintVirtualNetwork(virtualNetwork);
                }

                //============================================================
                // Delete a virtual network
                Utilities.Log("Deleting the virtual network");
                await azure.Networks.DeleteByIdAsync(virtualNetwork2.Id);
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

                await RunSample(client);
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
            }
        }
    }
}