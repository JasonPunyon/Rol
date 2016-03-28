# Rol

Rol is a C# library that makes storing and working with data in redis as easy as declaring an interface, plus some other bells and whistles. It's built on StackExchange.Redis and Sigil.

##Getting started

You can install Rol from nuget...

```powershell
PM> Install-Package Rol
```

##The Store

The Store is how you get at your data in redis. Rol wraps StackExchange.Redis's ConnectionMultiplexer to do it's work, so that's all it needs.

```c#
var connection = StackExchange.Redis.ConnectionMultiplexer.Connect("localhost");
var store = new Rol.Store(connection);
```

##Your data

The Store is how you get at your data, but at what data are you getting? The way you tell Rol about the data you want to store is by defining an interface that has the shape that you want. Let's say we're building a silly question and answer website and we want to store our question data. Questions have an integer Id, a Title ("How do I X?"), and a Body with the detail of the question. We'd define this interface...

```c#
public interface IQuestion
{
    int Id { get; }
    string Title { get; set; }
    string Body { get; set; }
}
```

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
