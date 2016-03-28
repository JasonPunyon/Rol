# Rol

Rol is a C# library that makes storing and working with data in redis as easy as declaring an interface, plus some other bells and whistles. It's built on StackExchange.Redis and Sigil.

##Getting started

You can install Rol from nuget...

```powershell
PM> Install-Package Rol
```

##Your data

The way you tell Rol about the data you want to store is by defining an interface that has the shape that you want. Let's say we're building a silly question and answer website and we want to store our question data. Questions have an integer Id, a Title ("How do I X?"), and a Body with the detail of the question. We'd define this interface...

```c#
public interface IQuestion
{
    int Id { get; }
    string Title { get; set; }
    string Body { get; set; }
}
```

..and Rol does the rest. By which I mean:

1. Rol lazily implements a concrete type that implements the IQuestion interface.
2. The properties on that concrete type read and write data from redis using StackExchange.Redis. They calculate where data belongs in redis based on the interface type (IQuestion), the Id of the particular instance you're working with, and the name of the property.
3. Rol lazily implements all the functions the Store needs to work with the data.

To create a question we go to the Store...

```c#
var question = store.Create<IQuestion>(); //returns an IQuestion
```

And to store the data in redis, you just set the properties...

```c#
question.Title = "How do I X?";
question.Body = "I'm really interested in how to do X. I've tried Y and Z but they don't seem to be X. How do I do X?"
```

And you're done. The data's in redis. If you want to read back the properties from redis...just read the properties of the question object...

```c#
Console.WriteLine($"Question Id: {question.Id}");
Console.WriteLine($"Question Title: {question.Title}");
Console.WriteLine($"Question.Body: {question.Body}");
```

###Ids

Objects in the store are accessed by Id, so your interface must have a get-only Id property. Ids can be `int`s, `string`s, or `Guid`s.

##The Store

The Store is how you get at your data in redis. Rol wraps StackExchange.Redis's ConnectionMultiplexer to do it's work, so that's all it needs.

```c#
var connection = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost");
var store = new Rol.Store(connection);
```

##Store.Create<>()

`Store.Create<>()` takes an optional `id` argument so you can create objects by Id...

```c#
[Test]
public void InterfaceWithIntKeyCanBeCreated()
{
    var withIntId = Store.Create<IQuestion>(3);
    Assert.AreEqual(3, withIntId.Id);
}
```

If your interface's Id is an integer you can also omit the id argument, and Rol will work like a database table with an auto-incrementing primary key.

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

    
}
```

To be honest, right now the Create method isn't that useful for interface with Id types other than int. You'll want to use Store.Get<>()

##Store.Enumerate<>()

If you've got integer ids and you're letting Rol handle the creation of those Ids, you can use `Store.Enumerate<>()` to walk through those objects in Id order.

```c#
[Test]
public void CreatedIntegerIdsIncrease()
{
    var first = Store.Create<IQuestion>();
    var second = Store.Create<IQuestion>();
    var third = Store.Create<IQuestion>();       

    var questions = Store.Enumerate<IQuestion>().ToList();

    Assert.IsTrue(questions.Select(o => o.Id).SequenceEqual(new[] { 1, 2, 3 }));
    Assert.AreEqual(3, Store.Enumerate<IQuestion>().Count());
}
```


##Store.Get<>()

Looks just like the create method but requires the id argument (Rol has to know what id you want to get).

```c#
[Test]
public void InterfaceWithStringKeyCanGetGot()
{
    var withStringId = Store.Get<IWithStringId>("Hello");
    Assert.AreEqual("Hello", withStringId.Id);
}

[Test]
public void InterfaceWithGuidKeyCanGetGot()
{
    var id = Guid.NewGuid();
    var withGuidId = Store.Get<IWithGuidId>(id);
    Assert.AreEqual(id, withGuidId.Id);
}
```
