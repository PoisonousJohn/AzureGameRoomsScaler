


# AzureGameRoomsScaler
Provides API to deploy new VMs and deallocate them considering the number of active game rooms

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FPoisonousJohn%2FAzureGameRoomsScaler%2Fmaster%2Fdeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

## Motivation

Dedicated game servers are stateful, thus they differ pretty much from typical statless VMs. You can't just shut down VM if somebody is playing at the moment. This project tries to solve the issue.

## Overview

AzureGameRoomsScaler is just a tiny application which can be plugged to your exising system which monitors server resources.

It will give you opportunity to:
- Roll out new VM based on a specific image
- Gracefully shutdown specific VM, waiting until there would be no game sessions running

To make gracefull shutdown work, you need to report game rooms number per VM when it changes.
