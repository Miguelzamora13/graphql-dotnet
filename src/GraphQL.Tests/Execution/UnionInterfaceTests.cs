using GraphQL.DI;
using GraphQL.Types;
using GraphQL.Utilities;
using GraphQL.Validation;

namespace GraphQL.Tests.Execution;

public class UnionInterfaceTests : QueryTestBase<UnionSchema>
{
    private readonly Person _john;

    public UnionInterfaceTests()
    {
        var garfield = new Cat
        {
            Name = "Garfield",
            Meows = false
        };

        var odie = new Dog
        {
            Name = "Odie",
            Barks = true
        };

        var liz = new Person
        {
            Name = "Liz",
            Pets = new List<IPet>(),
            Friends = new List<INamed>()
        };

        _john = new Person
        {
            Name = "John",
            Pets = new List<IPet>
            {
                garfield,
                odie
            },
            Friends = new List<INamed>
            {
                liz,
                odie
            }
        };
    }

    public override void RegisterServices(IServiceRegister register)
    {
        register.Transient<DogType>();
        register.Transient<CatType>();
        register.Transient<PetType>();
        register.Transient<PersonType>();
        register.Transient<NamedType>();
        register.Singleton<UnionSchema>();
    }

    [Fact]
    public void can_introspect_on_union_and_intersection_types()
    {
        const string query = """
            query AQuery {
              Named: __type(name: "Named") {
                kind
                name
                fields { name }
                interfaces { name }
                possibleTypes { name }
                enumValues { name }
                inputFields { name }
              }
              Pet: __type(name: "Pet") {
                kind
                name
                fields { name }
                interfaces { name }
                possibleTypes { name }
                enumValues { name }
                inputFields { name }
              }
            }
            """;

        const string expected = """
            {
              "Named": {
                "kind": "INTERFACE",
                "name": "Named",
                "fields": [
                  { "name": "name" }
                ],
                "interfaces": null,
                "possibleTypes": [
                  { "name": "Dog" },
                  { "name": "Cat" },
                  { "name": "Person" }
                ],
                "enumValues": null,
                "inputFields": null
              },
              "Pet": {
                "kind": "UNION",
                "name": "Pet",
                "fields": null,
                "interfaces": null,
                "possibleTypes": [
                  { "name": "Dog" },
                  { "name": "Cat" }
                ],
                "enumValues": null,
                "inputFields": null
              }
            }
            """;

        AssertQuerySuccess(query, expected);
    }

    [Fact]
    public void executes_using_union_types()
    {
        // NOTE: This is an *invalid* query, but it should be an *executable* query.    <--- ???

        const string query = """
            query AQuery {
              __typename
              name
              pets {
                __typename
                name
                barks
                meows
              }
            }
            """;

        const string expected = """
            {
              "__typename": "Person",
              "name": "John",
              "pets": [ null, null ]
            }
            """;

        var result = AssertQueryWithErrors(query, expected, root: _john, rules: Enumerable.Empty<IValidationRule>(), expectedErrorCount: 2);
        result.Errors[0].Message.ShouldBe("Error trying to resolve field 'pets'.");
        result.Errors[0].InnerException.Message.ShouldBe("Schema is not configured correctly to fetch field 'barks' from type 'Cat'.");
        result.Errors[1].Message.ShouldBe("Error trying to resolve field 'pets'.");
        result.Errors[1].InnerException.Message.ShouldBe("Schema is not configured correctly to fetch field 'meows' from type 'Dog'.");
    }

    [Fact]
    public void executes_union_types_with_inline_fragments()
    {
        // This is the valid version of the query in the above test.

        const string query = """
            query AQuery {
              __typename
              name
              pets {
                __typename
                ... on Dog {
                  name
                  barks
                },
                ... on Cat {
                  name
                  meows
                }
              }
            }
            """;

        const string expected = """
            {
              "__typename": "Person",
              "name": "John",
              "pets": [
                { "__typename":  "Cat", "name": "Garfield", "meows": false },
                { "__typename":  "Dog", "name": "Odie", "barks": true }
              ]
            }
            """;

        AssertQuerySuccess(query, expected, root: _john);
    }

    [Fact]
    public void executes_using_interface_types()
    {
        // NOTE: This is an *invalid* query, but it should be an *executable* query.    <--- ???

        const string query = """
            query AQuery {
              __typename
              name
              friends {
                __typename
                name
                barks
                meows
              }
            }
            """;

        const string expected = """
            {
              "__typename": "Person",
              "name": "John",
              "friends": [ null, null ]
            }
            """;

        var result = AssertQueryWithErrors(query, expected, root: _john, rules: Enumerable.Empty<IValidationRule>(), expectedErrorCount: 2);
        result.Errors[0].Message.ShouldBe("Error trying to resolve field 'friends'.");
        result.Errors[0].InnerException.Message.ShouldBe("Schema is not configured correctly to fetch field 'barks' from type 'Person'.");
        result.Errors[1].Message.ShouldBe("Error trying to resolve field 'friends'.");
        result.Errors[1].InnerException.Message.ShouldBe("Schema is not configured correctly to fetch field 'meows' from type 'Dog'.");
    }

    [Fact]
    public void allows_fragment_conditions_to_be_abstract_types()
    {
        const string query = """
            query AQuery {
              __typename
              name
              pets { ...PetFields }
              friends { ...FriendFields }
            }
            fragment PetFields on Pet {
              __typename
              ... on Dog {
                name
                barks
              },
              ... on Cat {
                name
                meows
              }
            }
            fragment FriendFields on Named {
              __typename
              name
              ... on Dog {
                barks
              },
              ... on Cat {
                meows
              }
            }
            """;

        const string expected = """
            {
              "__typename": "Person",
              "name": "John",
              "pets": [
                { "__typename":  "Cat", "name": "Garfield", "meows": false },
                { "__typename":  "Dog", "name": "Odie", "barks": true }
              ],
              "friends": [
                { "__typename":  "Person", "name": "Liz" },
                { "__typename":  "Dog", "name": "Odie", "barks": true }
              ]
            }
            """;

        AssertQuerySuccess(query, expected, root: _john);
    }
}

public interface INamed
{
    string Name { get; set; }
}

public interface IPet : INamed
{
}

public class Dog : IPet
{
    public string Name { get; set; }
    public bool Barks { get; set; }
}

public class Cat : IPet
{
    public string Name { get; set; }
    public bool Meows { get; set; }
}

public class Person : INamed
{
    public string Name { get; set; }
    public List<IPet> Pets { get; set; }
    public List<INamed> Friends { get; set; }
}

public class NamedType : InterfaceGraphType
{
    public NamedType()
    {
        Name = "Named";

        Field<StringGraphType>("name");
    }
}

public class DogType : ObjectGraphType<Dog>
{
    public DogType()
    {
        Name = "Dog";

        Field<StringGraphType>("name");
        Field<BooleanGraphType>("barks");

        Interface<NamedType>();
    }
}

public class CatType : ObjectGraphType<Cat>
{
    public CatType()
    {
        Name = "Cat";

        Field<StringGraphType>("name");
        Field<BooleanGraphType>("meows");

        Interface<NamedType>();
    }
}

public class PetType : UnionGraphType
{
    public PetType()
    {
        Name = "Pet";

        Type<DogType>();
        Type<CatType>();
    }
}

public class PersonType : ObjectGraphType<Person>
{
    public PersonType()
    {
        Name = "Person";

        Field<StringGraphType>("name");
        Field<ListGraphType<PetType>>("pets");
        Field<ListGraphType<NamedType>>("friends");

        Interface<NamedType>();
    }
}

public class UnionSchema : Schema
{
    public UnionSchema(IServiceProvider resolver)
        : base(resolver)
    {
        Query = resolver.GetRequiredService<PersonType>();
    }
}
