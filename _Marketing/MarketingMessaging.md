# The problem
Debugging misbehaving Unity transforms is not easy. Those can:
- change multiple times per frame
- change every frame in quick succession which makes it hard to pinpoint specific change that causes the issue
- different code can adjust any transform
- can be affected by physics


# Current Approach
Currently to get that sorted people have a lot of approaches that are simply not good enough:

## 'change multiple times per frame' and 'change every frame in quick succession which makes it hard to pinpoint specific change that causes the issue'
- attaching debugger - this is most reliable one although also most time consuming one
    - breakpoint is hit many times (could be 100s easily)
    - hard to tell just by looking at values what's wrong
    - time consuming to set up (set up conditional breakpoints and so on)


## different code can adjust any transform
- finding what's changing transform - this is done via some mixture:
    - of removing game object (to see null reference exceptions) 
    - looking for references in the scene

## can be affected by physics
- not sure, probably playing with physics, turning off and enabling back one by one to see what's happening


# Who has this issue / pain points

## Junior developers 
They are not fully aware what they are doing, hence looking for issue is really hard

- Unity does not offer good functionality to view what's happening with transforms, they just change and that's reflected on screen
- since they change really fast it's difficult to pinpoint exactly why
- rotation is especially difficult to understand with it's Qaternions (possibly that's a tool in it's own right? [Quaternion debugger?])
- I don't really know what's happening and this is purely confusing
- every time I make a change I need to recompile, takes ages

## Senior developers 
Senior developers working in bigger teams (where they may not have made the change / there's big system that can affect it). They'll have
different needs, generally want to get stuff done, no time to get stuck on some simple stuff - need to get next 'thing/feature/whatever' done

- There's a ton of systems that affect how object moves, it's quite hard to pinpoint quickly what's causing the issue
    - I do however know the system so I'll get to it at some point but it's tedious process
    - because of that I've adapted some code-practices to conteract (eg transforms are changed from central place / only by scripts on actual objects / more?)
- speed - compilation takes much more time, I've got a big project

# Name / Tagline
Visual Transform Changes debugger
any change, any tansform, any frame

# Introduction
Have you ever tried to debug transform that's not behaving as it should? 
- maybe in some random frames, position is not where it should be - making it 'jitter' a bit?
- perhaps its rotation is completely messed up?
- or scale goes all odd destroying the shape?

I bet you did and you've likely found that it's DIFFICULT to pinpoint the exact code/game-object that's causing the issue, as:
- transforms can be changed MULTIPLE times per frame and EVERY frame
- they can be changed by absolutely ANY object
- standard debugging methods are just not enough - attaching a debugger to find specific issue takes AGES, finding where to put a breakpoint is HARD and recompilation every time is simply FRUSTRATING 

I know, I've had to battle with those more times than I'd like to admit. This tool is a solution that'll change the way you troubleshoot from painful to simple. 


# Summary
Tool allows you to view all Transform Changes in an intuitive node-graph on a frame-by-frame basis. Enabling you to quickly identify code that caused issues and helps isolate exact code in play mode.

# Core Features addressing pain points

## View all Transform Changes in an intuitive node-graph on a frame-by-frame basis
Any change, any transform, any frame - displayed in an easy to navigate node-graph with powerful GUI that'll enable you to quickly identify hard to find transform issues.

No more guessing in your debugging process - every change will be recorded and displayed, you'll quickly get information about:

- Game Object that initiated change (eg. Mover-01)
    - with a single click you can see that object in Hierarchy View - this goes beyond just seeing script that causes the issue, you may be after a specific object instance as the issue could be due to individual setup
- Script and Method name
    - exact place in code where change originated (eg. HardToFindScript.cs - method: IntroduceTransformJitterJustToFrustrateDeveloper())
    - navigate directly to the code with a mouse click to find out why it's causing issues
- New Value
    - actual value that's been set, this makes it much simpler to pinpoint changes that are out of line
- Actual method Call and method arguments that caused the change
    - all methods/setters affecting transform will be captured
    - could be Transform.set_postion setter
    - or more complex Transform.Translate(float x, float y, float z)
    - whatever it is - you'll get those details

## Replay changes in Game/Scene View
Not only you can view and inspect changes in a friendly manner - you can go one step further and REPLAY them directly in Scene/Game view with a single click.

This means you don't have to focus on raw numbers you can simply and visually inspect suspicious changes and directly see if they cause the issue you're trying to find.

## Quickly identify and Temporaliry Disable specific Transform Modifiers to find the one that's causing issues
Tool groups changes by originating Game-Object-instance / Script and Method-name into Transform Modifiers
- this makes it easy for you to see at a glance which code is affecting the object in current frame-range

It also allows you to quickly turn them on/off via checkbox directly from gui with NO compilation needed!
- you can now instantly turn them off one by one and see in Game/Scene view how your tracked object is affected
- with that approach, it'll only take you few moments (instead of sometimes a few hours) to pinpoint that hard to find code that's causing the issue

## Very Simple Setup
Setup is pretty much non-existent:
1) import the asset
2) add 'TrackTransformChanges' script to objects you want to be tracked
3) hit play

That's it. No-fuss.

The tool is designed to integrate into your workflow seamlessly, you should see its load time under 0.5f second (and that is ONLY when you're using it).
 
## Well documented API allows you to extend beyond what I thought of

GUI is built on top of API that's well documented and allows you to programmatically:
- track specific transform changes
- skip changes based on passed predicate/s
- access changes data structures that can be grouped by object / frame / TransformModifier

With that, you can tackle more complex debugging scenarios when you have a better idea of what you're after. 

Have a look in docs pages to see what's avaialable:
https://immersiveVRTools.com/projects/transform-changes-debugger/documentation

## What is not supported (yet)
- physics originating changes - gravity / rigidbody movements (say via AddForce()). For now, it's best to temporarily turn rigidbody off when debugging

- changes made outside of the main thread

- methods changing more than a single transform property, eg 'SetPositionAndRotation' - will be captured as a position change (you'll still get correct information about method and method arguments)

- production builds - while this tool will work in production build it'll likely affect performance - it's been designed as an editor / debugging tool.

- NET Standard 2.0 API Compatibility level - currently you have to use .NET 4.x

## FAQ
- I'm getting compilation errors on load like: ILArrayGenerator.cs(26,85): error CS0246: The type or namespace name 'LocalBuilder' could not be found (are you missing a using directive or an assembly reference?)
** Tool supports

## Roadmap
- all items in 'What is not supported'
- improved Reply/Preview GUI for easier navigation
