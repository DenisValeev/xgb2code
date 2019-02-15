# Parse the data
library(readr)
Input <- read_delim("input.csv", "\t", escape_double = FALSE, locale = locale(decimal_mark = "."), na = "NULL", trim_ws = TRUE)
head(Input)
#View(Input)

# Train and validate
require(xgboost)
s <- sample.int(.5*nrow(Input), replace = F)
# Set the target column
target <- Input$y
# Remove the target and, if necessary, additional columns
drops <- tolower(c("y","applicationid"))
Input <- Input[, !(tolower(colnames(Input)) %in% drops), drop = F]
dtrain <- xgb.DMatrix(data = as.matrix(sapply(Input[s,], as.numeric)), label=target[s])
dtest <- xgb.DMatrix(data = as.matrix(sapply(Input[-s,], as.numeric)), label=target[-s])
watchlist <- list(train=dtrain, test=dtest)
bst <- xgb.train(data = dtrain, max_depth = 1, eta = 1, subsample = 1, colsample_bytree = 1, nthread = 16, nrounds = 60, objective = "binary:logitraw", missing = NA, watchlist=watchlist)

# Dump
xgb.dump(bst, "model.xgb")

# Feature importance
sink("importance.txt")
xgb.importance(colnames(Input), model = bst) 
sink()

# Column names
write.table(colnames(Input), file = "features.csv", append = F, quote = F, eol = "\t", row.names = F, col.names = T)
