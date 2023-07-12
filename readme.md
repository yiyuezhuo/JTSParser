Some helpers for JTS games, mainly extracted from a Unity Project so that I can test them in Python.

Build all solutions (library and a quick test):


```shell
dotnet build
```

Build library only:

```shell
dotnet build ./JTSParser
```

Test
```shell
dotnet run --project Showcase/ShowCase.csproj
```

Python Usage Example (by Python.Net):

```python
from pythonnet import load

load("coreclr", runtime_config=r"D:\agent\JTSParser\JTSParser\bin\Debug\net6.0\JTSParser.runtimeconfig.json")

import clr
from System import Reflection
Reflection.Assembly.LoadFile(r"D:\agent\JTSParser\JTSParser\bin\Debug\net6.0\JTSParser.dll")
import YYZ.JTS.NB

# with open(r"E:\JTSGames\Pen_spain\Scenarios\019.Maida_BrAI.scn") as f:
with open(r"E:\JTSGames\Pen_spain\Scenarios\011.Coruna4_BrAI.scn") as f:
    s = f.read()

scenario = YYZ.JTS.NB.JTSScenario()
scenario.Extract(s)
scenario.ToString()
```

Using .Netstandard 2.1

For somereason, `Python.Net` reject to load runtime according to file generated from .netstandard 2.1, but it can load runtime from the file from .net 6 then load .netstandard 2.1 library. For example:

```python
from pythonnet import load

load("coreclr", runtime_config=r"D:\agent\JTSParser\JTSParser\bin\Debug\net6.0\JTSParser.runtimeconfig.json")
# load("coreclr", runtime_config=r"D:\agent\JTSParser\JTSParser\bin\Debug\netstandard2.1\JTSParser.runtimeconfig.json")

import clr
from System import Reflection
# Reflection.Assembly.LoadFile(r"D:\agent\JTSParser\JTSParser\bin\Debug\net6.0\JTSParser.dll")
Reflection.Assembly.LoadFile(r"D:\agent\JTSParser\JTSParser\bin\Debug\netstandard2.1\JTSParser.dll")
# import YYZ.JTS.NB
from YYZ import JTS
```
