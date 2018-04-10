# Unity-GPU-Boids

This project was made to learn from compute shaders and to have a reference for similar project.<br>
It contains several implementations to see how some compare to others, check out the different folders in `Assets`.

<p align="center">
    <img src="docs/boids_preview.gif"/>
    <img src="docs/boids_affectors_preview.gif"/>
    <br>
</p>

Better resolutions previews: [Swarm](https://gfycat.com/OnlyGrossBluebird) - [Skull](https://gfycat.com/JauntyIgnorantKiskadee) - [I Love Unity](https://gfycat.com/SolidLateAmericanwigeon) - [Skull Mesh](https://gfycat.com/VengefulLeadingDuckbillcat) - [Upvote](https://gfycat.com/EssentialLimpingAntarcticfurseal) - [Moving Skull](https://gfycat.com/ImmediatePossibleGroundhog) - [Close Up Boids](https://gfycat.com/WindingWeepyGiraffe)

**Features**
- Flocking behaviour
- Parameters: speed, size, rotation, radius check...
- Skinned Mesh Boid animation data used on GPU
- Vertex frame interpolation
- Affectors with force and distance
- Convert data points to drawing
- Bitonic sorting

**How To Use**

Start the sample scene `AllFlocks` and run it. <br>
Try out the different implementations by toggling the different gameobjects. Mess around with the settings to see what you can do with it and move around the gameobject so that your boids will follow it.<br>
For custom drawings use my other project [PathToPoints](https://shinao.github.io/PathToPoints/) which converts an SVG file to a set of data points.

**Benchmarks**

Using a GTX 980 Ti

| Implementation | 1000 Boids | 4000 Boids | 32000 Boids
| --- | :---: | :---: | :---:
| **CPU Flock** | 20 FPS | 3 FPS | < 1 FPS
| **CPU Draw/GPU Compute** | 126 FPS | 14 FPS | < 1 FPS
| **GPU Flock** | > 1000 FPS | > 1000 FPS | 93 FPS
| **GPU Flock multilateration** | > 1000 FPS | 400 FPS | 42 FPS
| **GPU Flock bitonic sorting** | > 1000 FPS | 950 FPS | 20 FPS
| **GPU Flock skinned and affectors** | > 1000 FPS | > 1000 FPS | 80 FPS

It seems my tests to optimize with different implementations failed and a brute for loop seems to be faster than any other method. 

GPU Flock for each boid will check against every other boids if it's in its range, so we got a stable 32k loop every frame. Bitonic sorting on the other hand will average at 5k loop but still is slower, what's interesting it the fact that the bitonic sort does not seem to be the problem but the fact that each thread are accessing data at an offset instead at the beginning which means we have tons of cache miss on the GPU. Check out `Boids_Bitonic.compute` for more infos, will be glad to have some feedback on that.

**Compute Shaders**

A few tips and notes about compute shaders.<br>
Padding had a great impact on performance where I could increase my FPS by 10% at times. Strangely I read that padding to 16 bytes is what is suggested but in my experiments I had to add 4 to 8 additional bytes sometimes (see `Boid_Simple.compute` vs `Boid.compute`), anyone to shed light on this ?<br>
An array access (like MyStructuredBuffer[instanceId]) is really costly so when I had to access my buffer more than once I logically cached it in a variable, but some of the time it was more performant to access it again without caching it, probably will depend of the size of your struct and the number of time you access it.<br>
Do not use ComputeBuffer.GetData() it will tank your performance, try to like this project pass around values in buffers and things will become fast as hell. If you really have to then try out the experimental Async GetData().

**Future**

This GPU Flocking system is a great way to learn about compute shaders and is quite inexpensive to run for a few thousands units since it offload the work to the GPU and there is no readback to the CPU.<br>
With the arrival of ECS and the Jobs system in Unity and the already impressive ground work made with the [ECS flocking sample](https://github.com/Unity-Technologies/EntityComponentSystemSamples) I think both systems are quite equivalent though the ECS one will have the advantage of ease of expansion and debugging which might make me write the same features from this system to the ECS one.


**Requirements**
- Tested on Unity 2017+ - Should work from Unity 5.6
- Platform that supports compute shaders (PC & Console)

**Credits**
- [CPU Flocking by keijiro](https://github.com/keijiro/Boids)
- [Base conversion CPU Flocking to GPU by chenjd](https://github.com/chenjd/Unity-Boids-Behavior-on-GPGPU/)
