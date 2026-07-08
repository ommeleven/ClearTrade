# Level 5 (Guided) — Entity Framework Core

Format for every concept: **(1) what it is, in English** → **(2) a small generic example, unrelated to the brokerage project, showing the syntax in isolation** → **(3) a checklist of what to go change in your own codebase** — you write the actual code, then check items off.

---

## 5.1 — `async` / `await`

**Concept.** A normal C# method runs to completion on the calling thread — if it has to wait on something slow (a database, a network call), that thread just sits there blocked. Marking a method `async` and using `await` inside it lets the method pause at the slow operation and hand the thread back to the runtime, which can go do other work, then resume from exactly where it paused once the result is ready. The return type changes to `Task` (nothing meaningful to hand back) or `Task<T>` (a `T` will be available once it's done). Calling an `async` method requires `await`ing it — which forces the *caller* to become `async` too, propagating up the call chain.

**Generic example:**

```csharp
public async Task<string> FetchGreetingAsync(string name)
{
    await Task.Delay(1000);            // pretend this is 1 second of network latency
    return $"Hello, {name}!";
}

public async Task RunAsync()
{
    string result = await FetchGreetingAsync("Sam");   // thread is freed during the delay
    Console.WriteLine(result);
}
```

**Codebase checklist**
- [ ] In `Brokerage.Core/Interfaces/IAccountRepository.cs` (or `IRepository.cs` from Level 4): change every method to return `Task` or `Task<T>` instead of a direct value.
- [ ] In your Level 4 `InMemoryRepository<T>`: add `async` to each method, wrap return types in `Task<...>`, rename methods with an `Async` suffix.
- [ ] For the in-memory version specifically: use `Task.FromResult(...)` to return a value with nothing real to await — look up what it does before using it.
- [ ] In `AccountService`: add `await` before every repository call; change each surrounding method to `async Task<...>`.
- [ ] In the controller: add `await` before every service call; change each action method to `async Task<ActionResult<...>>`.

📍 **Checkpoint:** Every method from controller → service → repository is `async`, returns a `Task`/`Task<T>`, and the project still compiles.

---

## 5.2 — LINQ

**Concept.** LINQ is a query language built into C#, used the same way against an in-memory collection or, via EF Core, a database table. The core building block is a **lambda expression** — an inline function like `x => x.SomeProperty == value`. Chain methods like `.Where()` (filter), `.Select()` (project), `.OrderBy()` (sort), `.FirstOrDefault()` (get one or none), and `.ToList()`/`.ToListAsync()` (materialize results) to build a query.

**Generic example:**

```csharp
var numbers = new List<int> { 4, 8, 15, 16, 23, 42 };

var bigEvens = numbers
    .Where(n => n > 10 && n % 2 == 0)
    .OrderByDescending(n => n)
    .ToList();                          // [42, 16]

var firstBig = numbers.FirstOrDefault(n => n > 20);   // 23
```

**Codebase checklist**
- [ ] Find every place in your repository/service code that currently loops manually or filters a `List<Account>`.
- [ ] Identify the matching LINQ method for each: lookup-by-id → `FirstOrDefaultAsync`, fetch-all → `ToListAsync`, existence check → `AnyAsync`, count → `CountAsync`.
- [ ] Rewrite each one against the (soon-to-exist) `DbSet<Account>`, keeping the lambda predicate itself nearly unchanged from your Level 2–4 version.

📍 **Checkpoint:** You can point to the exact LINQ method used for each repository operation and explain, out loud, what SQL it should roughly translate to.

---

## 5.3 — Nullable reference types (`Type?`)

**Concept.** A property or return type of `Account` promises the compiler the reference always points to something real; `Account?` explicitly documents "this can be absent," and the compiler then requires a null-check before you use its members.

**Generic example:**

```csharp
public Book? FindBook(List<Book> library, string title) =>
    library.FirstOrDefault(b => b.Title == title);

var found = FindBook(library, "Dune");
Console.WriteLine(found.Title);          // ⚠️ warns: found could be null

if (found is not null)
{
    Console.WriteLine(found.Title);      // ✅ safe
}
```

**Codebase checklist**
- [ ] Add `?` to the return type of every repository/service method that looks something up and might not find it.
- [ ] In each controller action that calls one of those methods, confirm there's an `if (result is null) return NotFound();` (or similar) before the result is used.
- [ ] Scan your IDE for nullability warnings across the service and controller classes; resolve each by either adding `?` to a type or adding a null check.

📍 **Checkpoint:** Zero nullability warnings remain in `Brokerage.Services` and `Brokerage.Api`.

---

## 5.4 — Object initializer syntax

**Concept.** `new SomeType { Property = value }` is shorthand for calling the constructor and then setting each named property, all as one expression — "here's the finished shape," rather than separate assignment statements.

**Generic example:**

```csharp
var car = new Car { Make = "Honda", Model = "Civic", Year = 2022 };
```

**Codebase checklist**
- [ ] No new change required — you've used this since Level 1's seed data.
- [ ] If you add any new sample data this level (e.g. test `Holding` rows), use this syntax deliberately rather than separate assignment lines.

---

## 5.5 — Attributes (`[Table]`, `[Key]`, `[Required]`, `[Column]`)

**Concept.** Square-bracket syntax above a class/property/method is an **attribute** — metadata read by a framework via reflection, not executable logic. `[Table("name")]` names the table, `[Key]` marks the primary key, `[Required]` maps to `NOT NULL`, `[Column(TypeName = "...")]` pins the exact SQL type. These are an alternative to fluent-API configuration in `OnModelCreating`.

**Generic example:**

```csharp
[Table("books")]
public class Book
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = "";

    [Column(TypeName = "decimal(10,2)")]
    public decimal Price { get; set; }
}
```

**Codebase checklist**
- [ ] Open `Account` and `Holding` in `Brokerage.Core/Models`.
- [ ] Decide per property: attribute or fluent API (from 5.6) — not both for the same property.
- [ ] Add `[Required]` to string properties that should never be empty (`OwnerName`, `Symbol`).
- [ ] Add `[Column(TypeName = "decimal(18,2)")]` to `Balance` and `AverageCost` **only if** you're not configuring precision via fluent API in the `DbContext`.
- [ ] Leave `[Key]` off `Id` unless you rename that property — EF Core auto-detects a property named `Id` as the primary key.

📍 **Checkpoint:** Every money field has precision configured exactly once (attribute or fluent API, not duplicated).

---

## 5.6 — The `DbContext`

**Concept.** A `DbContext` is your gateway to the database — like a JPA `EntityManager`/Hibernate `Session`. Each entity gets a `DbSet<T>` property. A `DbSet<T>` isn't a real in-memory collection — LINQ against it builds SQL rather than filtering data already in memory (deferred execution: nothing runs until you force a result, e.g. `.ToListAsync()`). The constructor takes `DbContextOptions<T>` and passes it to the base class, wired in through DI (Level 3). `OnModelCreating` is where fluent-API configuration lives.

**Generic example:**

```csharp
public class LibraryDbContext : DbContext
{
    public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options) { }

    public DbSet<Library> Libraries => Set<Library>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Library>().Property(l => l.Name).HasMaxLength(200);
    }
}
```

**Codebase checklist**
- [ ] Create `BrokerageDbContext` in `Brokerage.Data`, inheriting from `DbContext`.
- [ ] Add a constructor taking `DbContextOptions<BrokerageDbContext>`, passing it to `base(options)` — no other logic in the constructor.
- [ ] Add `DbSet<Account> Accounts => Set<Account>();` and the equivalent for `Holding`.
- [ ] Override `OnModelCreating`; inside it, call `.HasPrecision(18, 2)` on `Balance` and `AverageCost` — unless already handled via `[Column]` attributes in 5.5.

📍 **Checkpoint:** `BrokerageDbContext` compiles and exposes both `DbSet<T>` properties.

---

## 5.7 — The EF-backed repository

**Concept.** This is Level 4's payoff: you already have `IRepository<T>` and an `AccountService` depending only on that interface. Now write a second implementation backed by the real database — nothing above the repository layer should need to change. The generic constraint tightens to `where T : class, IEntity` (EF Core requires entity types to be reference types). `DbContext` tracks pending changes in memory; `SaveChangesAsync()` is what actually sends SQL, letting several staged changes commit together as one transaction.

**Generic example:**

```csharp
public class EfRepository<T> where T : class, IEntity
{
    private readonly DbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(DbContext db) { _db = db; _set = db.Set<T>(); }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

    public async Task AddAsync(T item)
    {
        _set.Add(item);                 // staged only — nothing sent yet
        await _db.SaveChangesAsync();   // this line executes SQL
    }
}
```

**Codebase checklist**
- [ ] Update `IRepository<T>` (from 5.1) so every method returns `Task`/`Task<T>` — one pass, don't redo it here if already done.
- [ ] Create `EfRepository<T>` in `Brokerage.Data`, implementing `IRepository<T>`, constrained `where T : class, IEntity`.
- [ ] Constructor takes `BrokerageDbContext`; store both the context and the result of `.Set<T>()`.
- [ ] Implement `GetAllAsync` → `ToListAsync()`.
- [ ] Implement `GetByIdAsync` → `FindAsync(id)`.
- [ ] Implement `AddAsync` → `Add` on the set, then `SaveChangesAsync` on the context.
- [ ] Implement `UpdateAsync` → `Update` on the set, then `SaveChangesAsync`.
- [ ] Implement `DeleteAsync` → find the item, `Remove` if found, then `SaveChangesAsync`.
- [ ] In `Program.cs`: change the DI registration from `InMemoryRepository<>` to `EfRepository<>` for `IRepository<>`.
- [ ] Confirm `AccountService` required zero changes because of this swap — if it did, the Level 4 interface boundary wasn't clean.

📍 **Checkpoint:** Swapping one registration line in `Program.cs` is the only change needed to move from in-memory to real database storage.

---

## 5.8 — Migrations

**Concept.** A migration is a version-controlled, generated schema change — like a Flyway/Liquibase script, but generated from your C# model. `dotnet ef` reflects over your `DbContext` and entities, diffs against the last migration snapshot, and writes a C# file with `Up()`/`Down()` methods. `database update` connects to the real database and runs pending `Up()` methods.

**Generic example (commands only, toy project names):**

```bash
dotnet ef migrations add InitialCreate --project LibraryData --startup-project LibraryApi
dotnet ef database update --project LibraryData --startup-project LibraryApi
```

**Codebase checklist**
- [ ] Add the `Microsoft.EntityFrameworkCore.Design` package to `Brokerage.Data`.
- [ ] Add a connection string for Postgres to `Brokerage.Api/appsettings.json`.
- [ ] In `Program.cs`, register the context: `builder.Services.AddDbContext<BrokerageDbContext>(...)` pointing at that connection string.
- [ ] Run `dotnet ef migrations add InitialCreate` from `Brokerage.Api`, with `--project Brokerage.Data` and `--startup-project .`.
- [ ] Open the generated file under `Brokerage.Data/Migrations/` and read `Up()` — confirm it creates `accounts` and `holdings` tables with the columns/precision you expect.
- [ ] Start a local Postgres instance (Docker is fine).
- [ ] Run `dotnet ef database update` with the same `--project`/`--startup-project` flags.

📍 **Checkpoint:** `dotnet run` starts cleanly, and a manual insert via Swagger survives an app restart (proof it's really hitting Postgres, not memory).

---

## Before moving to Level 6

- [ ] Explain why `GetByIdAsync` returns `Task<Account?>` rather than `Account?`.
- [ ] Point to the exact line in your `EfRepository<T>.AddAsync` that sends SQL, versus the line that only stages the change.
- [ ] Explain what changed in `Program.cs` to swap repositories, and confirm `AccountService` didn't need to change.
- [ ] Describe what a migration file contains and why it's committed to source control rather than applied by hand.
