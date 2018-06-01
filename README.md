# xgb2code
A converter for xgboost model dumps to any code (C#, Sql Server, Oracle, etc.) 

## Output code types
* Sql Server functions: scalar and table-valued
* Oracle function
* C# code for ultra-lightweight compiled assembly

## Requirements
* A dump of an xgboost model to convert
* LinqPad installed on your computer to easily run `.linq` scripts. Download here: http://www.linqpad.net/Download.aspx

## Limitations
* Current implementation has been tested to support only classification with numeric input values. 
* For the multi-class classification a table-valued function (Sql Server) is used to return a row of class log probabilities.
