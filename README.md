# Rol

Rol is a C# library that makes storing and working with data in redis as easy as declaring an interface, plus some other bells and whistles. It's built on [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) and [Sigil](https://github.com/kevin-montrose/Sigil). Thanks and High Fives to [Marc Gravell](https://twitter.com/marcgravell) and [Kevin Montrose](https://twitter.com/kevinmontrose) (and all the other contributors to those libraries) for some great shoulders to stand on.

I'm not nearly done with it yet, but it's already been extremely useful to me in some prototyping scenarios so I'm letting it into the wild to see what others think. Hit me on twitter @JasonPunyon or in the issues if you've got ideas. 

<3 and thanks for taking the time to look.

##Getting started

You can install Rol from nuget... [![NuGet Status](http://img.shields.io/nuget/v/Rol.svg?style=flat)](https://www.nuget.org/packages/Rol)

```powershell
PM> Install-Package Rol
```

##Your data

The way you tell Rol about the data you want to store is by declaring an interface. Let's say we're building a silly question and answer website and we want to store our question data. Questions have an integer Id, a Title ("How do I X?"), and a Body with the detail of the question. We'd declare this interface...

```c#
public interface IQuestion
{
    int Id { get; }
    string Title { get; set; }
    string Body { get; set; }
    int Score { get; set; }
}
```

..and Rol does the rest. By which I mean:

1. Rol lazily implements a concrete type that implements the IQuestion interface.
2. The properties on that concrete type read and write data from redis using StackExchange.Redis. They calculate where data belongs in redis based on the interface type (IQuestion), the Id of the particular instance you're working with, and the name of the property.
3. Rol lazily implements all the functions the Store needs to work with the data.

**Note To The Reader:** Rol doesn't require you to do anything other than provide this interface. You do not have to write any concrete types that implement the interface or any other boilerplate. This is one of Rol's defining features. Embrace it.

##The Store

The Store is how you get at your data in redis. The Store wraps StackExchange.Redis's ConnectionMultiplexer to do it's work, so that's all it needs.

```c#
var connection = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost");
var store = new Rol.Store(connection);
```

##Store.Get\<T>()
Rol requires very little ceremony to start working with your data. If you already have the Id for an object you'd like to work with, just `.Get` that Id...

Note that this kind of behavior is different than what you might expect in an ORM. Creating objects isn't necessary, you just ask Rol for what you want and Rol gives it to you.

```c#
[Test]
public void GetQuestionFromStore()
{
    var question = Store.Get<IQuestion>(42);
    Assert.AreEqual(42, question.Id); //The object has the id you provided.
    Assert.AreEqual(null, question.Title); //The object's properties have the default values for their types.
    Assert.AreEqual(null, question.Body);
    Assert.AreEqual(0, question.Score);
}
```

## What kinds of properties can I use in my interfaces?

###You can use your basic types...

`bool`, `bool?`, `int`, `int?`, `string`, `double`, `double?`, `long`, `long?`, `DateTime`

###References

Our question and answer site is pretty lame right now. We can't even tell you who asked the question. So let's say we've got our user data...

```c#
public interface IUser 
{
    int Id { get; } //Gotta have it.
    string Name { get; set; }
    int Reputation { get; set; }
}
```

And we'll update our question interface to...

```c#
public interface IQuestion
{
    int Id { get; }
    IUser Asker { get; set; }
    string Title { get; set; }
    string Body { get; set; }
}
```

And when our user asks the question we can just set the User property on the Question...

```c#
public static void AskQuestion(int userId, string qTitle, string qBody) 
{
    var user = Store.Get<IUser>(userId);
    var question = Store.Create<IQuestion>();
    question.Title = qTitle;
    question.Body = qBody;
    question.Asker = user; //Isn't it just magical?
}
```

###Redis Collections

You can add redis collections to your interfaces by using the `IRedisSet<T>`, `IRedisHash<TKey, TValue>`, `IRedisSortedSet<T>`, and `IRedisHyperLogLog<T>` interfaces. Our questions need tags so they can be organized. Let's update our interface.

```c#
public interface IQuestion
{
    int Id { get; }
    IUser Asker { get; set; }
    string Title { get; set; }
    string Body { get; set; }
    IRedisSet<string> Tags { get; set; }
}

public static void AskQuestion(int userId, string qTitle, string qBody, string qTags) 
{
    var user = Store.Get<IUser>(userId);
    var question = Store.Create<IQuestion>();
    question.Title = qTitle;
    question.Body = qBody;
    question.Asker = user; //Isn't it just magical?
    foreach (var tag in qTags.Split(' '))
    {
        question.Tags.Add(tag);
    }
}
```

The keys and values of your collections can be references as well. Questions need answers, right?

```c#
public interface IAnswer 
{
    int Id { get; }
    IUser Answerer { get; set; }
    IQuestion Question { get; set; }
    string Body { get; set; }
}

public interface IQuestion
{
    int Id { get; }
    IUser Asker { get; set; }
    string Title { get; set; }
    string Body { get; set; }
    IRedisSet<string> Tags { get; set; }
    IRedisSet<IAnswer> Answers { get; set; }
}

public static void AnswerQuestion(int questionId, int userId, string aBody) 
{
    //And I don't think I even have to write you code to add the answer.
    //You can probably guess it, because everything's just getting objects and setting properties.
    
    //But I'll write it anyway :)
    var user = Store.Get<IUser>(userId);
    var question = Store.Get<IQuestion>(questionId);
    var answer = Store.Create<IAnswer>();
    answer.Body = aBody;
    answer.User = user;
    answer.Question = question;
    question.Answers.Add(answer);
}
```

###Async properties

You can tap into Rol's async support by declaring your properties of type `Async<T>`. Async<T>'s are awaitable just like tasks, and they're convertible to Task<T>, and they're convertible from their underlying types. Let's say we wanted to access the Title data of our IQuestion asynchronously...

```c#
public interface IQuestion 
{
    int Id { get; }
    string Title { get; set; }
    Async<string> TitleAsync { get; set; }
}

public async Task WorkWithTitle(int questionId) 
{
    var q = Store.Get<IQuestion>(questionId);
    q.Title = "This is a great title, isn't it?"; //Set the title synchronously.
    
    var title = await q.TitleAsync; //Await the title asynchronously
    Console.WriteLine(title); //"This is a great title, isn't it?";
    
    q.TitleAsync = "New Title!"; //T's are implicitly convertible to Async<T> so we can set properties values asynchronously. Rol handles everything under the covers.
    
    title = q.Title; //Read the title synchronously
    Console.WriteLine(title); //"New Title!"
    
    //Let's say we had a jillion questions and we wanted to pull their titles out of redis asynchronously...
    var titleTasks = Store.Enumerate<IQuestion>().ToDictionary(o => o.Id, o => o.TitleAsync);
    
    store.WaitAll(titleTasks.Values.ToArray()); //Wait for all the underlying tasks to complete.
    
    var titles = titleTasks.ToDictionary(o => o.Key, o => o.Value.Result);
}
```

###POCOs

Because let's face it, sometimes a property is a few fields together. POCOs get JSON serialized. The thing to watch out for is that changes made to the POCO don't get sent back to redis, you have to reset the POCO property on your interface instance for it to be persisted.

```c#
public class Location
{
    public double Lon { get; set; }
    public double Lat { get; set; }
}

public interface IUser
{
    int Id { get; }
    string Name { get; set; }
    int Reputation { get; set; }
    Location Location { get; set; }
}

public static void WorkWithLocation(int userId, double lon, double lat)
{
    var user = Store.Get<IUser>(userId);
    var location = new Location { Lon = lon, Lat = lat };
    user.Location = location;
    
    location.Lon = 3.0; //Nothing written to redis.
    Console.WriteLine(user.Location.Lon); //It'll be the value of lon.
    user.Location = location; //The entire location object now gets serialized and persisted.
    Console.WriteLine(user.Location.Lon); //3.0
}
```

##RolNameAttribute

One problem with storing objects in hashes the way Rol does is the overhead of type and property names. To put it ridiculously...

```c#
public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName
{
    int Id { get; }
    Async<string> StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName { get; set; }
}
```

...interfaces like that will waste a goodly amount of valuable redis memory repeating those type and property names in every object's hash.

To mitigate the problem, you can use the `RolName` attribute to give Rol shorter names to use for those properties in redis.

```c#
[RolName("i")]
public interface IInterfaceThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName
{
    int Id { get; }
	[RolName("s")]
    Async<string> StringPropertyThatIsOverlyDescribedByItsReallyRidiculouslyOverlyVerboseAndLongAndRedundantName { get; set; }
}
```

**Shoot Yourself In The Foot Warning**: Rol's gonna do what you tell it. So if you tell Rol to do something like this...

```c#
public interface IStuff 
{
    int Id { get; }
    
    [RolName("HAHA")]
    int StuffInt { get; set; }
    
    DateTime HAHA { get; set; }
}
```
...Rol will faithfully follow you into oblivion, no questions asked.

#You made it all the way down here! Well done!

More to come. Until then, enjoy yourself a tasty beverage and thank you for reading.
