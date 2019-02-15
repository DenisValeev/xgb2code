<Query Kind="Statements" />

// Получаем код
@"
+ case when a_2 < 1900235.25 then 217 else 0 end 
+ case when a_2 < 73102.5938 then 33 else 0 end 
+ case when a_2 < 8152.16016 then -50 else 0 end 
+ case when a_2 >= 426383.25 then -44 else 0 end 



".Replace("a_2", "x").Replace("case when", "(").Replace("then", "?").Replace("else", ":").Replace("end", ")").Dump();

Func<int, int> f = (x) => {
//x *= 1000;
x *= 1000;
	return 0
	

+ ( x < 1900235.25 ? 217 : 0 ) 
+ ( x < 73102.5938 ? 33 : 0 ) 
+ ( x < 8152.16016 ? -50 : 0 ) 
+ ( x >= 426383.25 ? -44 : 0 ) 



;};

Enumerable.Range(1, 100).ToList().Select(x => new {x, y = f(x)})
.Dump()
.Chart(c => c.x, c => c.y)
.Dump();