# The problem
Iterating on code takes too long:
- kills play mode, have to reenter
- full domain reload and compile - takes time

# Current Approach
Currently to get that sorted people have to 
- disable domain reloading (which sometimes break stuff)
- make small scenes to iterate quicker
- create code bits in small parts (use asmdefs to support)

## disabled domain reloading
- that gives quite good results but stuff can break (in odd places), like not initialized statics. Hard to pinpoint. A lot of the time, especially in VR I have to turn that off as it breaks framework (MRTK / Mirror)

## breaks your 'zone' when that hapenns 
- few second reload just breaks it for me, as it does for other programmers I'm sure. You leave your 'zone' and it's irritating

# Who has this issue / pain points

## Junior developers 
They do a lot of compiling to see results. And it'll eat ton of time

## Senior developers 
Would be less compiling but in some scenarions, eg testing approach out - the shortest loop is best

# Name / Tagline
Fast Script Reload
1) Play 2) Make Code Change 3) See results

Iterate on your code insanely fast without breaking your play session. 

---
Are you tired of waiting for full domain-reload and script compilation every time you make a small code change?


Me too.


Tool will automatically compile only what you've changed and immediately hot-reload that into current play session.


You can iterate on whatever you're working on without reentering play mode over and over again.


Works with any code editor.


• Setup

1) Import

2) Play

3) Make Code Change

4) See results


It's that simple.


• One-off custom code executions on Hot-Reload

When you need to set the stage to test your feature out.


Add following methods to changed script:


| void OnScriptHotReload()

| {

| //do whatever you want to do with access to instance via 'this'

| }


| static void OnScriptHotReloadNoInstance()

| {

| //do whatever you want to do without instance

| //useful if you've added brand new type

| // or want to simply execute some code without |any instance created.

| //Like reload scene, call test function etc

| }



• Performance

It's a development tool, you're not supposed to ship with it! :)


Your app performance won't be affected in any meaningful way though.

Biggest bit is additional memory used for your re-compiled code.

Won't be visuble unless you make 100s of changes in same play-session.


• Few things to have in mind, limitations:


• Breakpoints in hot-reloaded scripts won't be hit, sorry!

- only for the scripts you changed, others will work

- with how quick it compiles and reloads you may not even need a debugger


• Creating new public methods

Hot-reload for new methods will only work with private methods (eg only called by changed code)


• Adding new fields

As with methods, adding new fields is not supported in play mode

You can however simply create local variable and later quickly refactor that out


• No IL2CPP support


• Other minor limitations

There are some other minor limitations, please consult full list


• Roadmap

- add debugger support for hot-reloaded scripts