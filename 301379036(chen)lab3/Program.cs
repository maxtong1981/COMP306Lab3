using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using _301379036_chen_lab3.Data;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using _301379036_chen_lab3.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddDefaultIdentity<IdentityUser>().AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddScoped<S3Service>();
builder.Services.AddDefaultAWSOptions(
    builder.Configuration.GetAWSOptions()
);
builder.Services.AddScoped<IDynamoDBContext>(
    serviceProvider =>
    {
        IAmazonDynamoDB client =
            serviceProvider
                .GetRequiredService<IAmazonDynamoDB>();

        return new DynamoDBContext(client);
    }
);
builder.Services.AddScoped<
    ICommentService,
    DynamoDbCommentService
>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
