# Rol

Rol is a C# library that makes storing and working with data in redis as easy as declaring an interface, plus some other bells and whistles. It's built on [StackExchange.Redis](https://github.com/StackExchange/StackExchange.Redis) and [Sigil](https://github.com/kevin-montrose/Sigil). Thanks and High Fives to [Marc Gravell](https://twitter.com/marcgravell) and [Kevin Montrose](https://twitter.com/kevinmontrose) (and all the other contributors to those libraries) for some great shoulders to stand on.

I'm not nearly done with it yet, but it's already been extremely useful to me in some prototyping scenarios so I'm letting it into the wild to see what others think. Hit me on twitter @JasonPunyon or in the issues if you've got ideas. 

<3 and thanks for taking the time to look.

## Getting started

You can install Rol from nuget... [![NuGet Status](http://img.shields.io/nuget/v/Rol.svg?style=flat)](https://www.nuget.org/packages/Rol)

```powershell
PM> Install-Package Rol
```

## Your data

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
3. Rol lazily implements all the functions the Store (***foreshadowing!***) needs to work with the data.

**Note To The Reader:** Rol doesn't require you to do anything other than provide this interface. You do not have to write any concrete types that implement the interface or any other boilerplate. This is one of Rol's defining features. Embrace it.

## The Store

The Store is how you get at your data in redis. The Store wraps StackExchange.Redis's ConnectionMultiplexer to do its work, so that's all it needs.

```c#
var connection = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost");
var store = new Rol.Store(connection);
```

The Store is magical. You only need one instance of the Store for your entire application. The Store is not a unit-of-work context object like you might find in an ORM. Spin it up once at the beginning of your app, stick it in a static variable and use it from whenever and wherever you please, on different threads, whatever.

### Store.Get\<T>()
Rol requires little ceremony to start working with your data. If you already have the Id for an object you'd like to work with, just `Store.Get` that Id...

```c#
[Test]
public void GetAndWorkWithQuestionFromStore()
{
	var question = Store.Get<IQuestion>(42);
	Assert.AreEqual(42, question.Id); //The object has the id you provided.
	Assert.AreEqual(null, question.Title); //The object's properties have the default values for their types.
	Assert.AreEqual(null, question.Body);
	Assert.AreEqual(0, question.Score);
	
	//To write data to redis, just set the properties on the object.
	question.Title = "How do I X?";
	question.Body = "I'm trying to X. I've tried Y and Z but they're not X. How do I X?";
	
	//To read data from redis, just read the properties on the object.
	var title = question.Title;
	var body = question.Body;
	
	Assert.AreEqual("How do I X?", title);
	Assert.AreEqual("I'm trying to X. I've tried Y and Z but they're not X. How do I X?", body);
}
```

Notice:

1. There's no explicit object creation step. You can ask the Store to Get any value of the interface's Id type and Rol will hand you back an object. I could have done this and it would've worked.

    ```c#
    var question = Store.Get<IQuestion>(new Random().Next());
    ```

1. Also, there was no explicit "Read some stuff from Redis" operation. Every time you get a property's value, a read is issued to redis.
1. Also, there was no explicit "Write some stuff to Redis" operation. Every time you set a property's value, a write is issued to redis.

### Store.Create\<T>() / Store.Enumerate\<T>()

If your interface's Id is an `int`, the Store can provide you a "database table with an auto-incrementing primary key"-like experience by using `.Create<T>()` and `.Enumerate<T>()`.

```c#
[Test]
public void CreatedIntegerIdsIncrease()
{
    var first = Store.Create<IQuestion>();
    var second = Store.Create<IQuestion>();
    var third = Store.Create<IQuestion>();            

    Assert.AreEqual(1, first.Id);
    Assert.AreEqual(2, second.Id);
    Assert.AreEqual(3, third.Id);

    Assert.IsTrue(Store.Enumerate<IQuestion>().ToList().Select(o => o.Id).SequenceEqual(new[] { 1, 2, 3 }));
    Assert.AreEqual(3, Store.Enumerate<IQuestion>().Count());
}
```

## What kinds of properties can I use in my interfaces?

### You can use your basic types...

`bool`, `bool?`, `int`, `int?`, `string`, `double`, `double?`, `long`, `long?`, `DateTime`

### References

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

### Redis Collections

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
    answer.Answerer = user;
    answer.Question = question;
    question.Answers.Add(answer);
}
```

### IRedisArray\<T>

The `IRedisArray<T>` interface provides an array-like (O(1) access by index) collection for fixed size elements.

`T` can be: `int`,`double`,`short`,`long`,`float`,`ushort`,`uint`,`ulong`,`char`,`DateTime`,`Guid` or your interface type with an integer Id.

```c#
[Test]
public void IntRedisArrayProperty()
{
    var withProps = Store.Get<IWithProperties>(1);
    var val = new Random().Next();
    
    //Write a value to the integer array.
    withProps.IntArray[1] = val;
    
    //Read a value from the integer array.
    Assert.AreEqual(val, withProps.IntArray[1]);
}
```

Notice that we never had to give the array dimension, it will automatically grow as necessary up to 512MB in size.

### POCOs

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

### Async properties

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
    
    q.TitleAsync = "New Title!"; //T's are implicitly convertible to Async<T> so we can set property values asynchronously. Rol handles everything under the covers.
    
    title = q.Title; //Read the title synchronously
    Console.WriteLine(title); //"New Title!"
    
    //Let's say we had a jillion questions and we wanted to pull their titles out of redis asynchronously...
    var titleTasks = Store.Enumerate<IQuestion>().ToDictionary(o => o.Id, o => o.TitleAsync);
    
    store.WaitAll(titleTasks.Values.ToArray()); //Wait for all the underlying tasks to complete.
    
    var titles = titleTasks.ToDictionary(o => o.Key, o => o.Value.Result);
}
```

### RedisTTL

Adding a property of type `RedisTTL` allows you to expire your objects.

```c#
public interface IExpires
{
    int Id { get; }
    RedisTTL TTL { get; }
    string Hello { get; set; }
}

[Test]
public void ExpirationTests()
{
    var expires = Store.Get<IExpires>(1);
    expires.Hello = "World";

    Assert.IsNull(expires.TTL.Get());
    Assert.IsNotNull(expires.Hello);

    expires.TTL.Set(DateTime.UtcNow);
    Assert.IsNull(expires.TTL.Get());
    Assert.IsNull(expires.Hello);

    expires.Hello = "World";
    expires.TTL.Set(DateTime.UtcNow.Date.AddDays(1));
    Assert.IsNotNull(expires.TTL.Get());
}
```

## RolNameAttribute

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

## CompactStorageAttribute

If your interface type has an integer Id you can take advantage of massive memory savings (at the cost of lost readability of/interactibility with the keyspace by humans) on fixed size properties by marking them with the `[CompactStorage]` attribute.

```c#
public interface IWantSomeMemorySavings
{
    int Id { get; }
    int RegularIntProperty { get; set; }
    
    [CompactStorage] 
    int CompactIntProperty { get; set; }
}
```

By default Rol stores your interface types in Hashes, with property names as the fields, and property values as the values in the hash. This incurs a lot of overhead because we must repeat property names in every object's hash.

Using the `[CompactStorage]` attribute on a fixed size property tells Rol to store values for that property for all instances of that interface in a single `IRedisArray<TProperty>` indexed by Id. This eliminates the duplication of property names and saves scads of memory.

## Equality

A fun constraint in C# is that the only way for instances of interfaces to be considered equal is if they're the same object. Equality is important. You need equality to work for things to work like you want with Generic Collections and in other places.

Rol makes equality work the only way it can, by guaranteeing you only get the same instance of an object per Type/Id combination. It does this by caching instances of objects forever.

**Note for the future**: Infinitely growing caches is bad, especially if you don't need them, but I really like Equality working. I'm going to add an attribute to turn off the caching if you don't need it for certain types (so you can opt in to memory-savings/broken equality if you want), but by default it will be on for sanity.

#You made it all the way down here! Well done!

More to come. Until then, enjoy yourself a tasty beverage and thank you for reading.
