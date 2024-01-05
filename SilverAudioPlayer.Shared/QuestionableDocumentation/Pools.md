# Pools

What is a pool?
A pool as defined in SharedPools.cs is an object which contains a list of objects of a given disposable type and a list of references to those objects

Pools may be used in order to cache data such as bitmaps.
Say you have duck.png and know ahead of time that duck.png's hash is A1B2C3
Before loading duck.png you should always check the pool to see if it's already loaded (by checking if `SharedMemoryTPoolInstance<Image>.Instance.ContainsHash(A1B2C3)`) 

If it isn't then load duck.png and give it to the pool alongside its hash `var duckInPool = SharedMemoryTPoolInstance<Image>.Instance.AddT(myLoadedDuck,A1B2C3);` and create a reference `var referenceToMyDuck = new RelianceOnShared<Image>(duckInPool);`
Do not dispose myLoadedDuck, If you no longer need myLoadedDuck you can tell the pool so by disposing your reference to it `referenceToMyDuck.Dispose();`

If it is in the pool instead of loading it you can do `var duckInThePool = SharedMemoryTPoolInstance<Image>.Instance.GetWithHash(A1B2C3);` to save some RAM and CPU time, after that create a reference `var referenceToMyDuck = new RelianceOnShared<Image>(duckInPool);`
You can access `var myLoadedDuck=duckInPool.Object;` , don't dispose it manually, dispose referenceToMyDuck (`referenceToMyDuck.Dispose();`) when you no longer need myLoadedDuck

Once all references to a shared object are disposed then it is disposed

SharedMemoryStreamPool uses a similar system but hashes in the object are replaced with random guids, and hashing is used to determine whether a picture and another are the same or not, and additional logic for sharing streams has been used.
