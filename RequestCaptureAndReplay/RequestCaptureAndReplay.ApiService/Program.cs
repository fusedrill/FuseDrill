var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => "API service is running.");

app.MapPost("/ApiWithExernalRequest", async (ExampleRequest? exampleRequest) =>
{
    // call an external API
    var httpClient = new HttpClient();
    var response = await httpClient.GetStringAsync("https://postman-echo.com/get");

    return new ExampleResponse(
        Id: 1,
        Date: DateOnly.FromDateTime(DateTime.Now),
        reponse: response,
        Summary: "Sample data from external API call");
})
.WithName("ApiWithExernalRequest");

app.MapDefaultEndpoints();
app.Run();

record ExampleResponse(int Id, DateOnly Date, string reponse, string? Summary);
record ExampleRequest(int Id, string Name);
