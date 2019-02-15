<Query Kind="Program" />

void Main()
{
	Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");
	string modelFolder = null;
	var root = Path.GetDirectoryName(Util.CurrentQueryPath) + modelFolder;
	if(modelFolder == null)
		root = new DirectoryInfo(root).GetDirectories().OrderByDescending(di=>di.LastAccessTime).FirstOrDefault().FullName.Dump("Model");	
	var outputFile = Path.Combine(root, "model.sql").Dump("Output");
	Directory.SetCurrentDirectory(root);
	var isOracle = false;
	var isBodyOnly = true;
	var rawFeatures = File.ReadLines(Path.Combine(root, "features.csv")).First();
	var features = rawFeatures.Split('	', ';', ',').Skip(1).Where(f => f.Length > 0).Select(f=>f.Replace(" ", "_").Replace(".", "_")).Select(f=>((isOracle && char.IsDigit(f[0])) ? "R_" : "") + f).ToList();
	if(features.Count == 0)
		for(int i=0; i<100; i++)
			features.Add("f" + i.ToString());
	var boosterCount = 0;
	var numClasses = 1;
	var maxDepth = 0;
	var boosters = new List<Dictionary<int, String>>();
	foreach (var line in File.ReadLines(Path.Combine(root, "model.xgb")))
	{
		if(line.StartsWith("booster")){
			boosterCount++;
			continue;
		}
		if (boosters.Count() < boosterCount)
			boosters.Add(new Dictionary<int, String>());
		var splitLine = line.Trim().Split(':');
		boosters[boosterCount-1].Add(int.Parse(splitLine[0]), splitLine[1]);
	}
	
	var code = isOracle ? GenerateOracleCode(features, boosters, numClasses, maxDepth, isBodyOnly) : GenerateSqlServerCode(features, boosters, numClasses, maxDepth, isBodyOnly);
	
	code.Dump();
	File.WriteAllText(outputFile, code);
}

string GenerateOracleCode(List<string> features, List<Dictionary<int, String>> boosters, int numClasses, int maxDepth, bool isBodyOnly = false)
{
	var sb = new StringBuilder();
	
	var pDef = "";
	var lpDef = "";
	pDef = ""; lpDef = "";
	for(int i = 0; i < numClasses; i++)
		pDef += String.Format("@p{0} float, ", i);
	for(int i = 0; i < numClasses; i++)
		lpDef += String.Format("@lp{0} float, ", i);			
	pDef = pDef.Substring(0, pDef.Length-2);
	lpDef = lpDef.Substring(0, lpDef.Length-2);
	
	sb.AppendFormat("create or replace function \"XGB_{0}\" ( \n", DateTime.Now.ToString("yyyyMMdd_HHmm"));
	for(int i = 0; i < features.Count; i++)
		sb.AppendFormat("    {0} number default null{1}\n", features[i], i < features.Count-1 ? ", " : "");
	sb.Append(@") return number
as pragma autonomous_transaction;
	lp0 number;
begin
    lp0 := 0;
");
	
	for(int j = 0; j < numClasses; j++){
		for(int i = j; i < boosters.Count(); i+=numClasses){
			sb.AppendLine();
			sb.AppendFormat("    lp{0} := lp{0} + ", j);
			sb.Append(GenerateCaseCode(features, boosters[i], 0, 0).Replace("@", "").Trim(' '));
			sb.Append(";");
			if(maxDepth > 0)
				sb.Append(String.Format("where {0} <= @MaxDepth", i));
		}
	}
	sb.AppendLine();
	sb.AppendLine();
	sb.AppendLine("    return lp0;");
	sb.AppendLine("end;");
	return sb.ToString();
}

string GenerateSqlServerCode(List<string> features, List<Dictionary<int, String>> boosters, int numClasses, int maxDepth, bool isBodyOnly = false)
{
	var sb = new StringBuilder();
	
	var pDef = "";
	var lpDef = "";
	pDef = ""; lpDef = "";
	for(int i = 0; i < numClasses; i++)
		pDef += String.Format("@p{0} float, ", i);
	for(int i = 0; i < numClasses; i++)
		lpDef += String.Format("@lp{0} float, ", i);			
	pDef = pDef.Substring(0, pDef.Length-2);
	lpDef = lpDef.Substring(0, lpDef.Length-2);
	
	if(!isBodyOnly)
	{
		sb.Append(@"declare ");
		for(int i = 0; i < features.Count; i++)
			sb.AppendFormat("@{0} float{1}", features[i], i < features.Count-1 ? ", " : "");
		sb.AppendLine();
		sb.AppendLine();
		if(maxDepth > 0)
			sb.AppendLine("declare @MaxDepth int = " + maxDepth.ToString());

		sb.Append("declare @xgb table (" + (numClasses > 1 ? "predClass int, ": ""));
		sb.Append(pDef.Replace("@", "") + ", ");
		sb.Append(lpDef.Replace("@", "") + ")");
		sb.AppendLine();
		sb.AppendLine();
		sb.AppendLine(@"declare " + lpDef.Replace("float", "float = 0"));
		sb.AppendLine(@"declare " + pDef.Replace("float", "float = 0"));
	}
	
	for(int j = 0; j < numClasses; j++){
		for(int i = j; i < boosters.Count(); i+=numClasses){
			sb.AppendLine();
			sb.AppendFormat("select @lp{0} += ", j);
			sb.Append(GenerateCaseCode(features, boosters[i], 0, 0));
			if(maxDepth > 0)
				sb.Append(String.Format("where {0} <= @MaxDepth", i));
		}
	}
	
	if(!isBodyOnly)
	{
		sb.AppendLine();
		sb.AppendLine();
		var pStatement = "select ";
		for(int i = 0; i < numClasses; i++)
			pStatement += String.Format("@p{0} = 1/(1+exp(-@lp{0})), ", i);
		pStatement = pStatement.Substring(0, pStatement.Length-2);
		sb.Append(pStatement);
		if (numClasses > 1)
		{
			sb.AppendLine();
			sb.AppendLine();
			sb.AppendLine("declare @predClass int, @predP float, @predLP float");
			sb.AppendLine("declare @argmax table (class int, p float, lp float)");
			sb.Append("insert into @argmax values ");
			var argMaxValues = "";
			for(int i = 0; i < numClasses; i++)
				argMaxValues += String.Format("({0}, @p{0}, @lp{0}), ", i);
			argMaxValues = argMaxValues.Substring(0, argMaxValues.Length-2);
			sb.AppendLine(argMaxValues);
			sb.AppendLine("select top 1 @predClass = class, @predP = p, @predLP = lp from @argmax order by lp desc");
		}
		sb.AppendLine();
		sb.AppendFormat("insert into @xgb ({0}{1}, {2})\n", numClasses > 1 ? "predClass, predP, predLP, ":"", pDef.Replace("@", "").Replace(" float", ""), lpDef.Replace("@", "").Replace(" float", ""));
		sb.AppendFormat("select {0}{1}, {2}\n", numClasses > 1 ? "@predClass, @predP, @predLP, ":"", pDef.Replace(" float", ""), lpDef.Replace(" float", ""));
		sb.AppendLine();
		sb.AppendLine("select * from @xgb");
	}
	return sb.ToString();
}

string GenerateIfCode(Dictionary<int, String> booster, int nodeid, int indent = 0)
{
    if (booster[nodeid].StartsWith("leaf"))
        return String.Format("{0}s +={1};\n", new String('\t', indent), booster[nodeid].Split('=')[1]);

	//[b<-1] yes=1,no=2,missing=1 -> [b<-1] | yes=1,no=2,missing=1
	var splitLine = booster[nodeid].Split(' ');
    var expr = splitLine[0].Replace("[", "").Replace("]", ""); //[b<-1] -> b<-1
	var ynm = splitLine[1].Split(','); //yes=1,no=2,missing=1 -> yes=1 | no=2 | missing=1
	
	var yes = int.Parse(ynm[0].Split('=')[1]); //yes=1 -> 1
	var no = int.Parse(ynm[1].Split('=')[1]); //no=2 -> 2
	var missing = int.Parse(ynm[2].Split('=')[1]); //missing=1 -> 1
    
	var sb = new StringBuilder();
	//если yes == missing, значит нужно на else его помещать и делать отрицание expr
	sb.Append(String.Format("{0}if ({1}({2})) {{\n", new String('\t', indent), (yes == missing ? "!" : ""), expr));
	sb.Append(GenerateIfCode(booster, (yes == missing ? no : yes), indent + 1));
	sb.Append(String.Format("{0}}} else {{\n", new String('\t', indent)));
	sb.Append(GenerateIfCode(booster, (yes == missing ? yes : no), indent + 1));
	sb.Append(String.Format("{0}}}\n", new String('\t', indent)));
    return sb.ToString();
}

string GenerateCaseCode(List<string> features, Dictionary<int, String> booster, int nodeid, int indent = 0)
{
    if (booster[nodeid].StartsWith("leaf"))
        return String.Format("{0} ", booster[nodeid].Split('=')[1]);

	//[b<-1] yes=1,no=2,missing=1 -> [b<-1] | yes=1,no=2,missing=1
	var splitLine = booster[nodeid].Split(' ');
    var expr = splitLine[0].Replace("[", "").Replace("]", "").Split('<'); //[b<-1] -> b<-1
	expr[0] = features[int.Parse(expr[0].Substring(1, expr[0].Length - 1))];
	expr[1] = double.Parse(expr[1]).ToString();
	var ynm = splitLine[1].Split(','); //yes=1,no=2,missing=1 -> yes=1 | no=2 | missing=1
	
	var yes = int.Parse(ynm[0].Split('=')[1]); //yes=1 -> 1
	var no = int.Parse(ynm[1].Split('=')[1]); //no=2 -> 2
	var missing = int.Parse(ynm[2].Split('=')[1]); //missing=1 -> 1
    
	var sb = new StringBuilder();
	//если yes == missing, значит нужно на else его помещать и делать отрицание expr
	sb.AppendFormat("case when @{0}{1}{2} then ", expr[0], (yes == missing ? " >= " : " < "), expr[1]);
	sb.Append(GenerateCaseCode(features, booster, (yes == missing ? no : yes), indent + 1));
	sb.AppendFormat("else ");
	sb.Append(GenerateCaseCode(features, booster, (yes == missing ? yes : no), indent + 1));
	sb.AppendFormat("end ");
    return sb.ToString();
}