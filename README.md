


# AzureGameRoomsScaler
Allows to scale VMs considering the number of active game rooms

<a href="https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2FPoisonousJohn%2FAzureGameRoomsScaler%2Fmaster%2Fdeploy.json" target="_blank">
    <img src="http://azuredeploy.net/deploybutton.png"/>
</a>

## Motivation

Dedicated game servers are stateful, thus they differ pretty much from typical statless VMs. You can't just shut down VM if somebody is playing at the moment. This project tries to solve the issue.

## Overview

AzureGameRoomsScaler is just a tiny application which will send a custom metric `Game rooms number` to Azure, so one can leverage that metric when setting up scale rules.

This metric can be useful in both scale out and scale in cases.
