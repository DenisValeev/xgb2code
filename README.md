# xgb2code
A converter for xgboost model dumps to any code (C#, Sql Server, Oracle, etc.) 

## Output code types
* Sql Server functions: scalar and table-valued
* Oracle function
* C# code for an ultra-lightweight compiled assembly

## Requirements
* A dump of an xgboost model to convert
* LinqPad installed on your computer to easily run `.linq` scripts. Download here: http://www.linqpad.net/Download.aspx

## Limitations
* Current implementation has been tested to support only classification with numeric input values. 
* For the multi-class classification a table-valued function (Sql Server) is used to return a row of class log probabilities.

## XGBoost Tutorial

1. Get the data from sql, copy and paste to a csv file
``` sql
select *
from TrainTestData
order by newid()
```
2. Read the csv file in R Studio
``` R
library(readr)
TrainTestData <- read_delim("TrainTestData.csv", "\t", escape_double = FALSE, locale = locale(decimal_mark = ","), na = "NULL", trim_ws = TRUE)
View(TrainTestData)
```
3. Train and watch
``` R
require(xgboost)
dtrain <- xgb.DMatrix(data = as.matrix(sapply(TrainTestData[1:1000,1:4], as.numeric)), label=as.matrix(TrainTestData[1:1000,5]))
dtest <- xgb.DMatrix(data = as.matrix(sapply(TrainTestData[1001:1736,1:4], as.numeric)), label=as.matrix(TrainTestData[1001:1736,5]))
watchlist <- list(train=dtrain, test=dtest)
bst <- xgb.train(data = dtrain, max_depth = 1, eta = 0.1, colsample_bytree = 1, nthread = 4, nrounds = 12, objective = "reg:linear", missing = NA, watchlist=watchlist, eval_metric = 'mae')
```
4. Dump the model
``` R
xgb.dump(bst, "TrainTestDataModel-1-12.dump")
```
5. Feature importance
``` R
xgb.importance(model = bst)
```

## Keywords

xgboost, xgboost converter, xgb2sql, xgboost2sql, xgboost2cs, xgb2cs, xgboost2sas, xgb2sas, xgboost model converter, xgboost to c#, xgboost to sql, xgboost to oracle, xgbtosql, xgbtosql
