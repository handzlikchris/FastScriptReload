## Limitations

### Adding new fields
- sometimes when adding new fields you'll get some odd behaviour, this is generally happening when adding values before existing ones (in code)
eg
```
	[SerializeField] FunctionLibrary.FunctionName function; //existing
	
	[SerializeField] private int _dynamicallyAddedField; //dynamically added

	[SerializeField] private int _testIterationCounter = 1; //existing
```

This is likely down to the approach taken by asset in which it'll add [jmp] instruction to old class, seems like modifying structure (as in 
new methods / fields) can create issues.

What can help is moving new fields to be declared after all existing variables - ideally do not add new fields.

eg
```
	[SerializeField] FunctionLibrary.FunctionName function;  //existing
	
	[SerializeField] private int _testIterationCounter = 1;  //existing
	
    [SerializeField] private int _dynamicallyAddedField; //dynamically added

```

- dynamically added fields will not run inline initializer, eg `private int _dynamicallyAddedField; = 1` will not initialize to 1, you have to do that in `OnScriptHotReload()` method
- dynamically added fields will only show in editor after full reload