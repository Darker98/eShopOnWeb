# eShopOnWeb Quality Gate

A strict, self-evaluable quality gate for **any** code change added to this codebase — new feature, bug fix, new API endpoint, new domain service, new external integration, or database schema change.

eShopOnWeb is an **ASP.NET Core Clean Architecture reference sample** using Ardalis libraries (Specification, GuardClauses, Result, ApiEndpoints), MediatR, AutoMapper, and EF Core 8. Several conventions below are followed by some projects and not others. Where this gate is stricter than existing code, **the gate wins** — new code must meet the bar even where surrounding code does not. Existing weak spots are labelled **(anti-pattern in repo)** so you copy the requirement, not the gap.

## How to use this gate

1. Implement the change.
2. **Declare your change's traits** in [Applicability & scope](#applicability--scope). That fixes the set of applicable requirements.
3. Walk every **applicable** requirement (A–P) and answer it honestly.
4. Fill in the **[Final pass/fail checklist](#final-passfail-checklist)**. Mark each non-applicable item **`N/A — no <trait>`** — scoping out is explicit, never silent.
5. Confirm the build and tests are green.

## Legend

- **MUST** — blocking. The gate fails if not satisfied (when applicable).
- **MUST NOT** — blocking prohibition.
- **SHOULD** — strongly expected; deviations require a written justification.
- **`· TRAIT`** — applies only if you declared that trait. No tag = **Core**, always applies.
- *Verify:* — how an evaluator confirms the item objectively.
- *Ref:* — the eShop file that establishes the pattern to follow.
- **(anti-pattern in repo)** — an existing gap to **not** copy.

---

## Applicability & scope

Declare your change's **traits**, then apply only the requirements those traits activate. **Core** requirements always apply.

```text
[ ] EP   — Adds or modifies an HTTP endpoint (PublicApi minimal endpoint, Web MVC controller, or Razor Page)
[ ] DB   — Adds or changes EF Core entities, DbContext, entity configurations, or migrations
[ ] SVC  — Adds or modifies an ApplicationCore service, command handler, or query handler
[ ] EXT  — Calls an external / third-party HTTP service (not another eShopOnWeb project)
[ ] AUTH — Adds or changes authentication or authorization logic
[ ] PII  — Processes customer personal data (names, addresses, email, payment info)
```

**Applicability matrix:**

| Trait | Activates MUST | Activates SHOULD |
|---|---|---|
| Core (always) | A1 A2 A3 C1 C2 E1 E3 F1 G1 H1 H2 I1 I2 J1 J3 K1 K2 K3 K4 | A4 C3 E4 G2 H3 H4 I3 I4 J2 K5 K6 K7 |
| EP | B1 B2 B3 F2 G3 | B4 G4 |
| DB | D1 D2 D3 D4 D5 | D6 |
| SVC | A5 E2 | A6 |
| EXT | C4 C5 E5 J5 | C6 C7 |
| AUTH | F3 F4 | F5 F6 |
| PII | P1 | P2 P3 |

**Pass rule:** the gate **PASSES** when every applicable MUST is satisfied. Mark every non-applicable item **`N/A — no <trait>`** in the checklist — never leave a row blank.

**Worked example — a new read-only PublicApi endpoint** (e.g. `GET /catalog-items/{id}`: no DB schema change, no external HTTP, no auth change, no PII) ticks **EP** only, so **Core + EP** apply: A1–A3, C1–C2, E1, E3, F1–F2, G1, G3, H1–H2, I1–I2, J1, J3, K1–K4 (Core MUST) plus B1–B3 (EP MUST). Everything else — D1–D6 (DB), A5/E2 (SVC), C4/C5/E5 (EXT), F3–F4 (AUTH), P1 (PII) — is `N/A` with the missing trait, and that is a complete, passing gate for that endpoint.

---

## A. Architecture & layer placement

- **A1 (MUST)** — Domain logic (business rules, aggregates, specifications, interfaces, guard extensions, exceptions) lives in **`ApplicationCore`**. Infrastructure concerns (EF Core, external HTTP, email sending) live in **`Infrastructure`**. Presentation logic lives in **`Web`** or **`PublicApi`**. Code must not bypass this layering.  
  *Verify:* no `Microsoft.EntityFrameworkCore` or `Infrastructure` namespaces referenced from `src/ApplicationCore/`.  
  *Ref:* `src/ApplicationCore/`, `src/Infrastructure/`, `src/Web/`, `src/PublicApi/`.

- **A2 (MUST)** — New services are exposed through an **interface** in `ApplicationCore/Interfaces/`; the implementation lives in `Infrastructure/` or the appropriate presentation project. DI registers the interface, not the concrete type.  
  *Verify:* the DI registration binds an interface to its implementation; callers inject the interface.  
  *Ref:* `src/Web/Configuration/ConfigureCoreServices.cs` lines 14–26.

- **A3 (MUST NOT)** — EF Core types (`DbContext`, `DbSet<T>`, `IQueryable<T>`, entity configurations, migration classes) and `Microsoft.EntityFrameworkCore` namespace references must not appear in `ApplicationCore`. The domain layer is persistence-ignorant.  
  *Verify:* grep `src/ApplicationCore/` for `EntityFrameworkCore` — no results.

- **A4 (SHOULD)** — Cross-cutting concerns (caching, logging, metrics) are added as a **decorator** that wraps an existing interface, registered in DI, rather than mixed into the core service implementation.  
  *Ref:* `src/Web/Services/CachedCatalogViewModelService.cs` — memory-cache decorator over `CatalogViewModelService`.

- **A5 (MUST · SVC)** — ApplicationCore services depend on `IRepository<T>` / `IReadRepository<T>` for persistence. They never inject `DbContext`, `EfRepository<T>`, or any Infrastructure type directly.  
  *Verify:* constructor parameters in `ApplicationCore/Services/` use only interfaces from `ApplicationCore/Interfaces/`.  
  *Ref:* `src/ApplicationCore/Services/BasketService.cs`, `src/ApplicationCore/Services/OrderService.cs`.

- **A6 (SHOULD · SVC)** — Expected domain failures (not found, business rule violation, empty state) are communicated as `Ardalis.Result<T>` variants (`Result.NotFound()`, `Result.Invalid(...)`, `Result.Success(value)`) rather than returning `null` or throwing for anticipated outcomes.  
  *Ref:* `src/ApplicationCore/Interfaces/IBasketService.cs` — `SetQuantities` returns `Result<Basket>`.

---

## B. HTTP endpoints

- **B1 (MUST · EP)** — New **PublicApi** endpoints follow the **Ardalis.ApiEndpoints** (`BaseEndpoint`) pattern: one class per endpoint, one `HandleAsync()` method, an `[HttpGet/Post/Put/Delete]` attribute, `[Authorize]` where required.  
  *Verify:* the endpoint class inherits from `BaseEndpoint`; it contains exactly one HTTP-method attribute.  
  *Ref:* any file in `src/PublicApi/CatalogItemEndpoints/`.

- **B2 (MUST · EP)** — New **Web** Razor Pages use the code-behind model (`OnGet`, `OnPost`, `OnPostAsync`). New MVC controllers inherit from the project's base controller and follow `[Route("[controller]/[action]")]`.  
  *Ref:* `src/Web/Controllers/OrderController.cs`, `src/Web/Pages/`.

- **B3 (MUST · EP)** — Every PublicApi endpoint defines a typed **request** class and a typed **response** class. Route parameters, query strings, and body fields are bound through these — never parsed from raw `HttpContext`, `Request.Form`, or `Request.Query` in the handler body.  
  *Ref:* `src/PublicApi/CatalogItemEndpoints/CatalogItemListPaged.ListPagedCatalogItemRequest.cs`.

- **B4 (SHOULD · EP)** — PublicApi endpoints that return collections include pagination metadata (page index, page size, total count) consistent with the `ListPagedCatalogItemResponse` pattern.  
  *Ref:* `src/PublicApi/CatalogItemEndpoints/CatalogItemListPagedEndpoint.cs`.

---

## C. Configuration & secrets

- **C1 (MUST)** — All new tunables live in a dedicated `*Settings` / `*Options` POCO bound via `services.Configure<T>(configuration.GetSection(nameof(T)))` or `services.Configure<T>(configuration)`. They are read through `IOptions<T>` or `IOptionsMonitor<T>`, never via raw `IConfiguration.GetValue()` scattered across business logic.  
  *Ref:* `src/Web/Configuration/ConfigureWebServices.cs` (`CatalogSettings`); `src/BlazorShared/BaseUrlConfiguration.cs`.

- **C2 (MUST NOT)** — API keys, passwords, JWT signing keys, or connection strings must **not** be hardcoded in source files or committed to `appsettings*.json`. Use **user-secrets** (a `UserSecretsId` in the `.csproj`) for local development; environment variables or Azure Key Vault for deployed environments.  
  *Verify:* grep for the new secret value in tracked files returns nothing; the host `.csproj` declares a `UserSecretsId`.  
  **(anti-pattern in repo):** `src/ApplicationCore/Constants/AuthorizationConstants.cs` — `JWT_SECRET_KEY` and `DEFAULT_PASSWORD` are hardcoded constants. **Do not add new secrets here.**

- **C3 (SHOULD)** — Required configuration keys fail fast at startup. Use `configuration.GetRequiredSection("Key")` (throws when absent) or `IOptions` data-annotation validation (`.ValidateDataAnnotations().ValidateOnStart()`) so a misconfigured deployment fails to start with a clear message rather than failing on the first request.

- **C4 (MUST · EXT)** — The base URL for any external HTTP service comes from a `*Options` class bound from configuration; enforced `https://`; `AllowAutoRedirect = false` on the `HttpClientHandler`.  
  *Ref (structural pattern):* `src/BlazorShared/BaseUrlConfiguration.cs`.

- **C5 (MUST · EXT)** — Per-attempt and total operation timeouts are defined in the `*Options` class. The total budget (per-attempt × max-attempts + backoff) must be strictly less than the governing deadline (request timeout, message lease).

- **C6 (SHOULD · EXT)** — A sandbox vs. production switch in configuration prevents non-production environments from calling a live third-party service.

- **C7 (SHOULD · EXT)** — The vendor API version the integration targets is recorded in `*Options` or the README and, where supported by the vendor, sent on every request via a version header or query parameter.

---

## D. Database & Entity Framework Core

- **D1 (MUST · DB)** — New aggregate root entities implement **`IAggregateRoot`** and inherit from **`BaseEntity`** (`src/ApplicationCore/Entities/BaseEntity.cs`) to get the `int Id { get; protected set; }` convention. Value-object and child entities inherit `BaseEntity` but do not implement `IAggregateRoot`.  
  *Ref:* `src/ApplicationCore/Entities/Basket.cs`, `src/ApplicationCore/Entities/Order.cs`.

- **D2 (MUST · DB)** — Entity configuration (column names, constraints, relationships, property access modes) lives in a dedicated **`IEntityTypeConfiguration<T>`** class under `src/Infrastructure/Data/Config/`. `CatalogContext.OnModelCreating` calls `builder.ApplyConfigurationsFromAssembly(...)` and contains no inline Fluent API.  
  *Verify:* no Fluent API calls inside `CatalogContext.OnModelCreating` for the new entity.  
  *Ref:* `src/Infrastructure/Data/Config/BasketConfiguration.cs`, `src/Infrastructure/Data/CatalogContext.cs`.

- **D3 (MUST · DB)** — Schema changes have a **new EF Core migration** generated with `dotnet ef migrations add <DescriptiveName>`. Migration names are timestamp-prefixed and describe the change in PascalCase (e.g., `AddOrderShippingAddress`, not `Update` or `Fix1`).  
  *Verify:* a migration file exists with timestamp prefix; name is descriptive; no existing migration was edited.

- **D4 (MUST · DB)** — Migrations must not contain seed data. All data seeding goes in `*ContextSeed` classes invoked from `Program.cs` startup.  
  *Ref:* `src/Infrastructure/Data/CatalogContextSeed.cs`.

- **D5 (MUST · DB)** — SQL Server production connection paths use **`.EnableRetryOnFailure()`**. In-memory databases are used only in tests.  
  *Verify:* every `UseSqlServer()` call in a non-test path includes the retry option.  
  *Ref:* `src/Web/Program.cs` lines 43, 48.

- **D6 (SHOULD · DB)** — Before merging, run `dotnet ef migrations script` and review the generated SQL for unintended table rebuilds, dropped columns with existing data, or missing indexes.

---

## E. Resilience & reliability

- **E1 (MUST)** — Every `async` method that performs I/O accepts a **`CancellationToken`** and forwards it to every awaitable call (`repository.ListAsync(spec, cancellationToken)`, `httpClient.GetAsync(url, cancellationToken)`, etc.).  
  *Verify:* no I/O-performing `async` method omits the token; grep changed code for `async` methods without a `CancellationToken` parameter.  
  **(anti-pattern in repo):** `src/ApplicationCore/Services/BasketService.cs` — `AddItemToBasket`, `DeleteBasketAsync`, `SetQuantities`, and `TransferBasketAsync` all omit `CancellationToken`. Do not copy this.

- **E2 (MUST · SVC)** — No sync-over-async: **never call `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()`** on a `Task` or `ValueTask` in service, controller, or endpoint code. Await tasks directly.  
  *Verify:* grep changed files for `\.Result` and `\.Wait()`.  
  **(anti-pattern in repo):** `src/BlazorAdmin/Services/CatalogItemService.cs` lines 45–75 — reads `.Result` on already-completed `Task.WhenAll` tasks. Do not copy. Use separate `await` calls or await the `Task.WhenAll` then immediately read the already-resolved tasks if you must.

- **E3 (MUST NOT)** — `new HttpClient()` per-request or per-method. All `HttpClient` instances must come from **`IHttpClientFactory`** or a DI-registered typed client (`AddHttpClient<T>()`).  
  *Verify:* grep production code for `new HttpClient()`.  
  **(anti-pattern in repo):** `src/Web/HealthChecks/ApiHealthCheck.cs` line 24 — creates `new HttpClient()` on every health check. Do not copy.

- **E4 (SHOULD)** — After `await Task.WhenAll(t1, t2, t3)`, read results via the individual awaited tasks (which are now complete), not via `.Result` as a habit. The pattern `var x = t1.Result` after a `WhenAll` await is technically safe but misleading — prefer assigning each task result in a separate await to make the async nature explicit.

- **E5 (MUST · EXT)** — Outbound calls to external HTTP services are protected by **retry + circuit breaker + bounded timeout**. Use a Polly resilience pipeline (`AddResilienceHandler()` on the typed client, or `ResiliencePipelineBuilder`) configured in the DI registration. Retry predicates must exclude 4xx responses; non-idempotent write operations carry an idempotency key before retrying.  
  *Verify:* a Polly policy (or equivalent) is wired in the service registration; the policy is exercised in a unit test with a stubbed 503.

---

## F. Security

- **F1 (MUST)** — Secrets, bearer tokens, session identifiers, and PII must **not** appear in log messages, exception messages, trace attributes, or API responses. Log boolean flags (`IsAuthenticated`, `IsValid`) or hashed/masked values — never the raw credential or PII field value.  
  *Verify:* inspect every `_logger.Log*()` call in changed files for sensitive-value interpolation.

- **F2 (MUST · EP)** — Endpoints that access user-specific data or perform privileged operations carry `[Authorize]` (MVC controllers), `.RequireAuthorization()` (minimal API), or `options.Conventions.AuthorizePage()` (Razor Pages).  
  *Verify:* any endpoint touching order history, basket checkout, admin catalog operations, or user management requires auth.  
  *Ref:* `src/Web/Controllers/OrderController.cs` line 11; `src/Web/Program.cs` line 89.

- **F3 (MUST · AUTH)** — JWT bearer configuration sets `RequireHttpsMetadata = true` in all non-Development environments. Never set it globally to `false`.  
  *Verify:* any `RequireHttpsMetadata = false` assignment is guarded by `builder.Environment.IsDevelopment()`.  
  **(anti-pattern in repo):** `src/PublicApi/Program.cs` line 61 — unconditional `RequireHttpsMetadata = false`. New code must not copy this.

- **F4 (MUST · AUTH)** — Cookie authentication settings baseline must not be weakened from the values in `ConfigureCookieSettings.cs` (`HttpOnly = true`, `SecurePolicy = Always`, `SameSite = Lax`). Stricter settings are allowed; looser settings are not.  
  *Ref:* `src/Web/Configuration/ConfigureCookieSettings.cs`.

- **F5 (SHOULD · AUTH)** — Authorization on admin-level operations uses a named policy or explicit role check rather than bare `[Authorize]` with no policy, so the permission requirement is self-documenting.

- **F6 (SHOULD · AUTH)** — Sensitive operations (deleting catalog items, modifying prices, managing users) are gated on an admin role check consistent with the existing BlazorAdmin admin-only pattern.

---

## G. Input validation

- **G1 (MUST)** — Inbound data is validated before any domain operation:
  - **MVC controllers**: check `ModelState.IsValid` and return early with a validation error response.
  - **Razor Pages**: check `ModelState.IsValid` in every `OnPost*` handler.
  - **PublicApi endpoints**: validate the request DTO before calling domain services.  
  *Verify:* every mutable endpoint has a `ModelState.IsValid` check (or equivalent guard clause) that runs before the first domain call.  
  *Ref:* `src/Web/Controllers/ManageController.cs` lines 69–74.

- **G2 (SHOULD)** — Domain service entry points use **`Ardalis.GuardClauses`** for preconditions: `Guard.Against.Null(...)`, `Guard.Against.NegativeOrZero(...)`, and custom guard extensions for domain-specific invariants.  
  *Ref:* `src/ApplicationCore/Services/OrderService.cs`; `src/ApplicationCore/Extensions/GuardExtensions.cs`.

- **G3 (MUST · EP)** — Request DTOs use **DataAnnotations** (`[Required]`, `[Range]`, `[StringLength]`) to constrain input shape. Annotations alone are insufficient — they must be evaluated by the model-binding pipeline; confirm `ModelState.IsValid` is checked in the handler.  
  *Ref:* `src/PublicApi/CatalogItemEndpoints/` request classes.

- **G4 (SHOULD · EP)** — PublicApi error responses use **RFC 7807 ProblemDetails** — `TypedResults.Problem(...)` or `TypedResults.ValidationProblem(...)` — not raw error strings or unstructured JSON.

---

## H. Error handling

- **H1 (MUST)** — Domain-level expected failures (entity not found, business rule violation, duplicate, empty state) are communicated as **typed exceptions** in `ApplicationCore/Exceptions/` (`BasketNotFoundException`, `DuplicateException`, `EmptyBasketOnCheckoutException`) or as `Ardalis.Result` failure variants. Never throw raw `Exception` or `InvalidOperationException` with an arbitrary message for anticipated domain outcomes.  
  *Ref:* `src/ApplicationCore/Exceptions/`.

- **H2 (MUST NOT)** — Raw exception messages (including `exception.Message`, EF Core error details, or stack traces) must not be serialized into API responses or user-facing error pages in non-Development environments.  
  *Verify:* `ExceptionMiddleware` or the equivalent catches and translates exceptions; no raw `exception.Message` reaches the response body in production.  
  **(anti-pattern in repo):** `src/PublicApi/Middleware/ExceptionMiddleware.cs` line 47 — writes `exception.Message` directly into the response. For new exception types that may carry internal infrastructure details, ensure the translation layer emits a safe, user-facing message.

- **H3 (SHOULD)** — New domain error conditions get a dedicated exception class in `src/ApplicationCore/Exceptions/` so `ExceptionMiddleware` can map them to the correct HTTP status code.  
  *Ref:* `DuplicateException` → HTTP 409; `BasketNotFoundException` → HTTP 404.

- **H4 (SHOULD)** — Optional or feature-flagged dependencies degrade gracefully: the service exposes an `IsEnabled` property or is replaced by a null-object so callers do not wrap optional functionality in try/catch.

---

## I. Observability & logging

- **I1 (MUST NOT)** — String interpolation in log message templates. Use **named structured-logging placeholders**:  
  ✓ `_logger.LogInformation("Updating item {ItemId} to quantity {Quantity}", item.Id, quantity)`  
  ✗ `_logger.LogInformation($"Updating item ID:{item.Id} to {quantity}.")`  
  *Verify:* grep changed files for `LogInformation($`, `LogWarning($`, `LogError($`, `LogDebug($`.  
  **(anti-pattern in repo):** `src/ApplicationCore/Services/BasketService.cs` line 57; `src/BlazorAdmin/Services/CatalogLookupDataService.cs` line 38; multiple BlazorAdmin files. Do not copy.

- **I2 (MUST)** — ApplicationCore services inject **`IAppLogger<T>`** (the project's logging abstraction), not `ILogger<T>` directly. This keeps the domain layer free of Microsoft.Extensions.Logging dependencies.  
  *Verify:* constructor parameters in `src/ApplicationCore/Services/` use `IAppLogger<T>`.  
  *Ref:* `src/ApplicationCore/Interfaces/IAppLogger.cs`; `src/Infrastructure/Logging/LoggerAdapter.cs`.

- **I3 (SHOULD)** — Significant operations (order created, basket transferred, checkout attempted) are logged at `Information` level with the relevant domain identifier (order id, buyer id) as a named placeholder.

- **I4 (SHOULD)** — PII values (customer email, full name, address) are not logged even at `Debug` level. Log an anonymized identifier (user id, masked string) instead.

---

## J. Testing

- **J1 (MUST)** — New **ApplicationCore** logic (services, specifications, domain entity behavior) has unit tests in `tests/UnitTests/` using **xUnit + NSubstitute**, following the AAA (Arrange / Act / Assert) layout. Test class and file names mirror the class under test and the scenario (`AddItemToBasket.cs`, `SetQuantities.cs`).  
  *Verify:* a `*Tests.cs` or scenario-named file exists for the changed service/entity.  
  *Ref:* `tests/UnitTests/ApplicationCore/Services/BasketServiceTests/AddItemToBasket.cs`.

- **J2 (SHOULD)** — Test data is constructed via **builder classes** in `tests/UnitTests/Builders/` rather than duplicating construction logic across tests.  
  *Ref:* `tests/UnitTests/Builders/BasketBuilder.cs`.

- **J3 (MUST)** — Tests cover **unhappy paths**: null input, empty collections, entity not found, domain rule violation, and the specific error condition introduced by the change. Assert the correct exception type or `Result` variant — never that no exception is thrown as a proxy for success.

- **J4 (SHOULD)** — New **PublicApi** endpoints have integration tests in `tests/PublicApiIntegrationTests/` or `tests/FunctionalTests/PublicApi/` using `WebApplicationFactory` with an in-memory database.  
  *Ref:* `tests/FunctionalTests/PublicApi/ApiTestFixture.cs`.

- **J5 (MUST · EXT)** — External HTTP calls are stubbed in tests via a fake `HttpMessageHandler` injected into the typed client. Retries are **disabled** in test `*Options` config. Timeout tests use zero or negligible timeouts so they fail fast, not after a real wait.  
  *Verify:* no test makes a real outbound HTTP call to a third-party service; a stubbed 503 fails within milliseconds.

---

## K. Code quality

- **K1 (MUST)** — **Async all the way**: every `async` method uses `await`; no `Task.Run()` wrappers to force async work onto a thread-pool thread inside ASP.NET Core request handlers.

- **K2 (MUST)** — `IDisposable` resources (`HttpResponseMessage`, `Stream`, database transactions) are disposed via `using` declarations or `using` statements. No fire-and-forget `Task`s that write to a disposed HTTP response.

- **K3 (MUST)** — New projects or new `.csproj` files enable **`<Nullable>enable</Nullable>`**. In existing projects: do not introduce new nullable-reference-type warnings; do not suppress them with the `!` (null-forgiving) operator to silence genuine null paths.  
  *Verify:* new project files contain `<Nullable>enable</Nullable>`; no new `!` operators on genuinely nullable expressions.

- **K4 (MUST)** — The solution builds cleanly: `dotnet build eShopOnWeb.sln` produces **zero new warnings** compared to the baseline on `main`.  
  *Verify:* run the build before submitting; diff the warning count.

- **K5 (SHOULD)** — New code follows `.editorconfig` conventions:
  - **File-scoped namespaces** required (`namespace Microsoft.eShopWeb.Web.Something;` — `.editorconfig` line 108 warns on violations).
  - **Private fields**: camelCase with `_` prefix (`_basketService`, not `basketService` or `BasketService`).
  - **`var`** preferred where the type is apparent from context.  
  *Ref:* `.editorconfig`.

- **K6 (SHOULD)** — New MediatR query/command handlers follow `GetMyOrders` / `GetMyOrdersHandler` naming and live in `src/Web/Features/<FeatureName>/`.  
  *Ref:* `src/Web/Features/MyOrders/GetMyOrdersHandler.cs`.

- **K7 (SHOULD)** — New AutoMapper mappings are added to `src/PublicApi/MappingProfile.cs`. Do not create a second profile or instantiate `new MapperConfiguration()` inline.  
  *Ref:* `src/PublicApi/MappingProfile.cs`.

---

## P. Privacy

- **P1 (MUST · PII)** — PII sent to an external service (customer name, email, address, phone, payment info) is limited to the **minimum fields required** for the operation. Full customer profiles are not forwarded by default.  
  *Verify:* review the outbound request payload field by field against "minimum necessary."

- **P2 (SHOULD · PII)** — Document where vendor-shared PII is stored, for how long, and on what legal basis. Order-processing records are typically retained for at least one year.

- **P3 (SHOULD · PII)** — When a customer account is deleted, document how PII shared with any external vendor is deleted or anonymized, or which legal-retention exemption applies.

---

## Final pass/fail checklist

Declare traits first, then fill this in. **Every applicable MUST must be checked.** Mark each non-applicable row **`N/A — no <trait>`**.

### MUST (blocking — when applicable)

```text
chk  trait  id   requirement
[ ] core   A1   Domain logic in ApplicationCore; infra in Infrastructure; presentation in Web/PublicApi
[ ] core   A2   Service behind interface; DI registers the interface not the concrete type
[ ] core   A3   EF Core / Microsoft.EntityFrameworkCore absent from ApplicationCore
[ ] SVC    A5   ApplicationCore services inject IRepository<T>/IReadRepository<T>, not DbContext
[ ] EP     B1   PublicApi endpoint inherits BaseEndpoint; one class, one HTTP method
[ ] EP     B2   Web Razor Pages use code-behind model; MVC controllers follow [Route] convention
[ ] EP     B3   Typed request + response classes; no raw HttpContext parameter parsing
[ ] core   C1   Tunables in *Settings/*Options POCO bound via Configure<T>
[ ] core   C2   No secrets hardcoded or in appsettings; user-secrets / env vars / Key Vault
[ ] EXT    C4   External base URL from config; HTTPS-only; AllowAutoRedirect = false
[ ] EXT    C5   Per-attempt + total timeout defined; total < governing deadline
[ ] DB     D1   New aggregate roots implement IAggregateRoot + inherit BaseEntity
[ ] DB     D2   Entity config in IEntityTypeConfiguration<T> in Infrastructure/Data/Config/
[ ] DB     D3   New EF Core migration with descriptive PascalCase name; no existing migration edited
[ ] DB     D4   No seed data in migrations; seeding in *ContextSeed
[ ] DB     D5   SQL Server paths use .EnableRetryOnFailure()
[ ] core   E1   CancellationToken accepted and forwarded in all async I/O methods
[ ] SVC    E2   No .Result / .Wait() / .GetAwaiter().GetResult() in service or controller code
[ ] core   E3   No new HttpClient() instances; IHttpClientFactory or typed clients only
[ ] EXT    E5   External HTTP calls wrapped with Polly retry + circuit breaker + timeout
[ ] core   F1   No secrets, tokens, or PII in logs, traces, exceptions, or API responses
[ ] EP     F2   Endpoints on user data or privileged ops carry [Authorize] / RequireAuthorization
[ ] AUTH   F3   RequireHttpsMetadata = true outside Development; not globally false
[ ] AUTH   F4   Cookie auth baseline (HttpOnly, Secure, SameSite) not weakened
[ ] core   G1   Inbound data validated before domain operation (ModelState.IsValid or equivalent)
[ ] EP     G3   Request DTOs have DataAnnotations; annotations evaluated by the validation pipeline
[ ] core   H1   Domain failures as typed exceptions or Ardalis.Result variants; no raw Exception
[ ] core   H2   Raw exception.Message not serialized in API responses in non-Dev environments
[ ] core   I1   Named structured-logging placeholders only; no string interpolation in log templates
[ ] core   I2   ApplicationCore services inject IAppLogger<T>, not ILogger<T> directly
[ ] core   J1   Unit tests in tests/UnitTests (xUnit + NSubstitute, AAA)
[ ] core   J3   Unhappy paths covered: null, not found, business rule violation, domain-specific error
[ ] EXT    J5   External HTTP stubbed via fake handler; retries disabled; fast timeouts in tests
[ ] core   K1   Async all the way; no Task.Run() in request handlers
[ ] core   K2   IDisposable resources disposed via using
[ ] core   K3   <Nullable>enable</Nullable> in new projects; no ! to silence genuine null warnings
[ ] core   K4   dotnet build eShopOnWeb.sln → zero new warnings vs. main
[ ] PII    P1   PII minimized: only required fields sent to external vendor
```

### SHOULD (justify any gaps — when applicable)

```text
chk  trait  id   requirement
[ ] core   A4   Cross-cutting concerns added as decorator; not mixed into service
[ ] SVC    A6   Expected domain failures returned as Ardalis.Result variants
[ ] EP     B4   Collection endpoints include pagination metadata
[ ] core   C3   Required config validated at startup; fails fast on missing/malformed keys
[ ] EXT    C6   Sandbox/test-mode switch; non-prod never calls live third-party
[ ] EXT    C7   Vendor API version documented and pinned in *Options
[ ] core   E4   Task.WhenAll results read after await; .Result used only on completed tasks
[ ] AUTH   F5   Named policies / role checks on admin-level ops; not bare [Authorize]
[ ] AUTH   F6   Admin operations gated on admin role check
[ ] core   G2   Guard.Against.* at service preconditions
[ ] EP     G4   Validation and error responses use RFC 7807 ProblemDetails
[ ] core   H3   New domain error condition has a typed exception class in ApplicationCore/Exceptions/
[ ] core   H4   Optional dependencies degrade gracefully (IsEnabled / null-object)
[ ] core   I3   Significant operations logged at Information with domain identifier
[ ] core   I4   No PII values in logs at any level
[ ] core   J2   Test data via builder classes; no magic literals scattered across tests
[ ] EP     J4   PublicApi endpoints have integration tests via WebApplicationFactory
[ ] core   K5   File-scoped namespaces; _ prefix fields; var for apparent types (.editorconfig)
[ ] core   K6   MediatR handlers in Features/<Name>/ with Get*/Handle* naming
[ ] core   K7   New AutoMapper mappings added to single MappingProfile.cs
[ ] DB     D6   Generated migration SQL reviewed for unintended rebuilds / dropped columns
[ ] PII    P2   Data residency and retention period documented
[ ] PII    P3   Account deletion propagation to external vendors documented
```

### Gate result

```text
Declared traits:  [ ] EP  [ ] DB  [ ] SVC  [ ] EXT  [ ] AUTH  [ ] PII
Build:   dotnet build eShopOnWeb.sln   → [ ] green
Tests:   dotnet test eShopOnWeb.sln    → [ ] green
Gate:    [ ] PASS (every applicable MUST checked)  [ ] FAIL (list unmet applicable MUSTs below)

Unmet applicable MUSTs:
  -
  -

Scoped out (N/A — list each non-applicable item and absent trait):
  -
  -
```

---

## Key reference files

| Concern | File | Notes |
|---|---|---|
| Layer structure | `src/ApplicationCore/`, `src/Infrastructure/`, `src/Web/`, `src/PublicApi/` | Clean architecture boundaries |
| Core DI registration | `src/Web/Configuration/ConfigureCoreServices.cs` | Repository + service registration (lines 14–26) |
| Web DI registration | `src/Web/Configuration/ConfigureWebServices.cs` | MediatR, view model services |
| Repository interfaces | `src/ApplicationCore/Interfaces/IRepository.cs`, `IReadRepository.cs` | Ardalis.Specification contracts |
| Repository implementation | `src/Infrastructure/Data/EfRepository.cs` | Generic `EfRepository<T>` |
| Specification pattern | `src/ApplicationCore/Specifications/BasketWithItemsSpecification.cs` | Ardalis.Specification query |
| Entity base class | `src/ApplicationCore/Entities/BaseEntity.cs` | `Id` property convention |
| Entity config | `src/Infrastructure/Data/Config/BasketConfiguration.cs` | `IEntityTypeConfiguration<T>` pattern |
| DbContext | `src/Infrastructure/Data/CatalogContext.cs` | `ApplyConfigurationsFromAssembly` in `OnModelCreating` |
| Guard clauses | `src/ApplicationCore/Extensions/GuardExtensions.cs` | Custom domain guard extension |
| Result pattern | `src/ApplicationCore/Interfaces/IBasketService.cs` (SetQuantities) | `Ardalis.Result<T>` usage |
| Domain exceptions | `src/ApplicationCore/Exceptions/` | `BasketNotFoundException`, `DuplicateException`, `EmptyBasketOnCheckoutException` |
| Exception middleware | `src/PublicApi/Middleware/ExceptionMiddleware.cs` | `DuplicateException` → 409 mapping |
| Cookie auth config | `src/Web/Configuration/ConfigureCookieSettings.cs` | `HttpOnly`/`Secure`/`SameSite` baseline |
| JWT auth config | `src/PublicApi/Program.cs` lines 55–70 | Bearer token setup |
| Auth (Razor Pages) | `src/Web/Program.cs` line 89 | `AuthorizePage` convention |
| Logger abstraction | `src/Infrastructure/Logging/LoggerAdapter.cs` | `IAppLogger<T>` → `ILogger<T>` adapter |
| Structured logging ✓ | `src/Web/Controllers/ManageController.cs` line 370 | Correct named-placeholder pattern |
| Cache decorator | `src/Web/Services/CachedCatalogViewModelService.cs` | Decorator pattern example |
| AutoMapper profile | `src/PublicApi/MappingProfile.cs` | Single profile convention |
| MediatR handler | `src/Web/Features/MyOrders/GetMyOrdersHandler.cs` | Feature folder + handler naming |
| DB seeding | `src/Infrastructure/Data/CatalogContextSeed.cs` | Separate from migrations |
| Test fixture | `tests/FunctionalTests/PublicApi/ApiTestFixture.cs` | `WebApplicationFactory` + in-memory DB |
| Unit test | `tests/UnitTests/ApplicationCore/Services/BasketServiceTests/AddItemToBasket.cs` | NSubstitute + AAA |
| Test builders | `tests/UnitTests/Builders/BasketBuilder.cs` | Fluent builder for test data |
| Central packages | `Directory.Packages.props` | All NuGet versions pinned centrally |
| Code style | `.editorconfig` | File-scoped namespaces (line 108), field naming (line 77) |
| **Anti-pattern** — hardcoded secrets | `src/ApplicationCore/Constants/AuthorizationConstants.cs` | `JWT_SECRET_KEY`, `DEFAULT_PASSWORD` hardcoded |
| **Anti-pattern** — `new HttpClient()` | `src/Web/HealthChecks/ApiHealthCheck.cs` line 24 | Socket exhaustion risk |
| **Anti-pattern** — `.Result` after WhenAll | `src/BlazorAdmin/Services/CatalogItemService.cs` lines 45–75 | Sync-over-async smell |
| **Anti-pattern** — string interpolation in logs | `src/ApplicationCore/Services/BasketService.cs` line 57 | Use named placeholders |
| **Anti-pattern** — `RequireHttpsMetadata = false` | `src/PublicApi/Program.cs` line 61 | Must be env-guarded |
| **Anti-pattern** — `exception.Message` in response | `src/PublicApi/Middleware/ExceptionMiddleware.cs` line 47 | May leak infrastructure details |
