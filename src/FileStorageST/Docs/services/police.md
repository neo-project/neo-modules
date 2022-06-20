# PoliceService

1. List local object addresses via *StorageEngine.List*
2. Check every object if the count of nodes stored the object is short than replication required in container placement policy 
3. If true, send a replication task including object adress, shortage and un-stored node infos to [ReplicateSerivce](./ReplicateService.md).

