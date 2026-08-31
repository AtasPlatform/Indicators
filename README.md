# ATAS Indicators

Full documentation, usage instructions, and code examples can be found at [docs.atas.net](https://docs.atas.net/).  
This includes detailed guidelines for working with the source code, implementation examples, and technical references.

---

## Build

1. Install [ATAS Platform](https://atas.net/Setup/ATASPlatform.exe) into `C:\Program Files (x86)\ATAS Platform\`
2. Install or update [NET10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
3. Clone repository and build it with Visual Studio 2026 or Rider.

## Possible Build Error

If you encounter the following error during project build:

```
The "GenerateDepsFile" task failed unexpectedly.
System.ArgumentException: An item with the same key has already been added. Key: ATAS.Indicators.Technical
   at System.Collections.Generic.CollectionExtensions.LibraryCollectionToDictionary[T](IReadOnlyList`1 collection)
   ...
```

This may happen due to a temporary conflict in dependency resolution.  
In most cases, it's enough to **wait a few seconds and try building the project again**.

If the problem persists, or you have any other problems please open the GitHub Issue.

---

Again, full documentation, usage instructions, and code examples can be found at [docs.atas.net](https://docs.atas.net/).  
This includes detailed guidelines for working with the source code, implementation examples, and technical references.
