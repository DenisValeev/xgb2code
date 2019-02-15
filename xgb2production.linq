<Query Kind="Statements" />

string modelFolder = null;
var root = Path.GetDirectoryName(Util.CurrentQueryPath) + modelFolder;
if(modelFolder == null)
	root = new DirectoryInfo(root).GetDirectories().OrderByDescending(di=>di.LastWriteTime).FirstOrDefault().FullName.Dump("Model");
var freq = @"
";
var importanceFile = new FileInfo(Path.Combine(root, "importance.txt"));
if(importanceFile.Exists)
	freq = File.ReadAllText(importanceFile.FullName);
var input = @"
";
var modelFile = new FileInfo(Path.Combine(root, "model.sql"));
if(modelFile.Exists)
	input = File.ReadAllText(modelFile.FullName);

var important = freq.Split('\n').Where(f=>f.Contains(":"))
.Select((l, i) => {
	var split = l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
	return new{
		Feature = string.Join(" ", split.Take(split.Count - 3)),
		Gain = Math.Round(100 * float.Parse(split[split.Count - 3]), 1),
		Frequency = Math.Round(100 * float.Parse(split[split.Count - 1]), 1),
		Index = i
	};
}).ToList();

string @when = "select @lp0 += case when @", @then = " then ", @else = " else ", @end = " end ";
var xgb = input.Replace(when, "").Replace(then, "\t").Replace(@else, "\t").Replace(@end, "").Split('\n').Select(s=>s.Split('\t')).Where(l=>l.Length > 1)
.Select(_ => new{Predicate = _[0], Left = float.Parse(_[1]), Right = float.Parse(_[2])})
.Select(_ => new { Feature = _.Predicate.Split(' ').First(), _.Predicate, Left = (int)(100 * (_.Left - _.Right))})
// Группируем предикаты по совпадающим условиям и суммируем результат в L
.GroupBy(_ => new { _.Feature, _.Predicate}).Select(_=>new {_.Key.Feature, _.Key.Predicate, Left = _.Sum(e=>e.Left)})
.ToList();

Util.AutoScrollResults = false;
var missing = xgb.Where(x=>!important.Any(i=>i.Feature == x.Feature));
if(missing.Any()) missing.Dump("Ошибка?");

var baseline = xgb.Where(p=>p.Left < 0).Sum(p=>-p.Left);//.Dump("base");
var result = string.Join("\n", xgb
.Join(important, lk=>lk.Feature, rk=>rk.Feature, (l, r) => new {l.Left, l.Predicate, r.Index, r.Gain}) //Оставляем только важные предикаты
.OrderBy(p=>p.Index).ThenBy(p=>p.Predicate)
//.Select(p=>$"{@when}{p.P}{@then}{p.L}{@else}0{@end} -- {p.G}%")
.Select(p=>$"+ case when {p.Predicate}{@then}{p.Left}{@else}0{@end}")
).Dump();

File.WriteAllText(Path.Combine(root, "production.sql"), result);