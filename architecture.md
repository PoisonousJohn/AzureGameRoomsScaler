# Architecture [DRAFT]
 
## This document contains information about the overall architecture of the project plus some FAQ.

The application heavily relies on an external service (probably the matchmaking service). This service will have the responsibility to call the described operations. Moreover, the application should have a storage mechanism (Table Storage?) to store information about running VMs. This should contain:

- VM unique ID (maybe its full ARM ID)
- VM state
- Public IP (if needed)
- whatever else is needed by the game (ports? if available, of course)

VM state should have one of the following values
- Creating
- Running
- MarkedForDeletion
- Deallocated

The API Function should have the following operations

- *CreateVM*. This would query our storage.
If there is at least one VM in the Creating state, the operation should exit.
Else if there is at least one VM in the MarkedForDeletion state, its state will change to Running and the operation should exit.
Else if there is at least one VM in the Deallocated state, then the operation will call ARM API to start this VM again and its state should change to Creating.
Else, this operation should use ARM API to create a new VM. The VM will be marked as Creating state in the storage. When it is created, there will be a mechanism that will update our storage with the new details of the new VM and set its status to Running. (mechanism will probably be Function + Event Grid)
- *GetNumberOfGameRooms(VM_ID)*. This would ask the VM (via a well-known REST API method) for the current running game rooms/sessions. If there are zero, this operation will call the DeallocateVM operation.
- *DeallocateVM(VM_ID)*. This will deallocate the VM via Azure ARM API and the associated line from the storage.
- *MarkVMForDeallocation(VM_ID)*. This will mark the VM as MarkedForDeallocation in the storage.
- *GetAvailableVMs*. This operation will return details about all VMs that are in the Running state.

## FAQ 

### *Since we are not deleting VMs but we are deallocating them, what happens with the application state?*
We take if for granted that when a VM (re)boots it clears all its pre-existing state.

### *What happens when we mark a VM as MarkedForDeallocation? When is it deallocated?*
The VM should be deallocated when there are no more running sessions on it. We take it for granted that an external (matchmaking?) service will determine that the VM is no longer needed to be alive.