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

1. Create a new folder with the model name of your choice as it will contain all the necessary files for the building process
2. Copy the template `model.r` file into the model folder and open with R Studio setting the model directory as current directory in R Studio
3. Get the data from sql, then copy and paste to `input.csv` file in the model folder
``` sql
select * from Data
```
4. Read the csv file and view data if necessary
``` R
library(readr)
Input <- read_delim("input.csv", "\t", escape_double = FALSE, locale = locale(decimal_mark = "."), na = "NULL", trim_ws = TRUE)
head(Input)
#View(Input)
```
5. Split the data into two equal sets: train and test
``` R
require(xgboost)
s <- sample.int(.5*nrow(Input), replace = F)
```
6. Specify the target column, then remove the target from the train and test data and, if necessary, remove additional columns that you don't want to train on, also the columns that correlate highly with the target (too much gain/importance)
``` R
target <- Input$y
drops <- tolower(c("y","applicationid"))
Input <- Input[, !(tolower(colnames(Input)) %in% drops), drop = F]
```
7. Train and watch! Tune the hyperparameters (`eta`, `subsample`, `colsample_bytree`), don't touch the `max_depth` as we want to train only tree stumps (single layer)
``` R
dtrain <- xgb.DMatrix(data = as.matrix(sapply(Input[s,], as.numeric)), label=target[s])
dtest <- xgb.DMatrix(data = as.matrix(sapply(Input[-s,], as.numeric)), label=target[-s])
watchlist <- list(train=dtrain, test=dtest)
bst <- xgb.train(data = dtrain, max_depth = 1, eta = 1, subsample = 1, colsample_bytree = 1, nthread = 16, nrounds = 60, objective = "binary:logitraw", missing = NA, watchlist=watchlist)
```
8. Dump the model to `model.xgb` in the model folder
``` R
xgb.dump(bst, "model.xgb")
```
9. Dump the feature importance to `importance.txt` in the model folder
``` R
sink("importance.txt")
xgb.importance(colnames(Input), model = bst) 
sink()
```
10. Dump the feature names to `features.csv` to use during the `xgb2code` conversion
``` R
write.table(colnames(Input), file = "features.csv", append = F, quote = F, eol = "\t", row.names = F, col.names = T)
```
11. Copy `xgb2code.linq` to the root folder (not the model folder) and run to convert `model.xgb` inside the model folder to `.sql`
12. Copy `xgb2production.linq` to the root folder and run to optimize the model `.sql`, grouping duplicate predicates and sorting on importance

## Keywords

xgboost, xgboost converter, xgb2sql, xgboost2sql, xgboost2cs, xgb2cs, xgboost2sas, xgb2sas, xgboost model converter, xgboost to c#, xgboost to sql, xgboost to oracle, xgbtosql, xgbtosql
