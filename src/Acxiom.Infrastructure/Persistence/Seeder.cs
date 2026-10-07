using Acxiom.Domain;
using Acxiom.Domain.Activities;
using Acxiom.Domain.Customers;
using Acxiom.Domain.FollowUps;
using Acxiom.Domain.Leads;
using Acxiom.Domain.Sales;
using Acxiom.Domain.Teams;
using Acxiom.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acxiom.Infrastructure.Persistence;

public static class Seeder
{
    public static async Task RunAsync(IServiceProvider root)
    {
        using var scope = root.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<CrmDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        await db.Database.MigrateAsync();

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles.All)
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var password = config["Demo:Password"];
        if (!bool.TryParse(config["Demo:Seed"], out var seed) || !seed || string.IsNullOrEmpty(password)) return;
        if (await db.Users.AnyAsync()) return;

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        async Task<AppUser> Add(string email, string name, string role)
        {
            var u = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, LockoutEnabled = true, CreatedAt = DateTime.Now.AddYears(-1) };
            var res = await users.CreateAsync(u, password);
            if (!res.Succeeded) throw new InvalidOperationException("Demo user: " + string.Join(", ", res.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(u, role);
            return u;
        }

        var admin = await Add("admin@acxiom.test", "Kiran Bhat", Roles.Admin);
        var meera = await Add("meera@acxiom.test", "Meera Iyer", Roles.Manager);
        var sameer = await Add("sameer@acxiom.test", "Sameer Khan", Roles.Manager);
        var ravi = await Add("ravi@acxiom.test", "Ravi Teja", Roles.SalesExecutive);
        var divya = await Add("divya@acxiom.test", "Divya Nair", Roles.SalesExecutive);
        var arjun = await Add("arjun@acxiom.test", "Arjun Das", Roles.SalesExecutive);

        var east = Team.Create("East Zone", meera.Id);
        var west = Team.Create("West Zone", sameer.Id);
        db.Teams.AddRange(east, west);
        await db.SaveChangesAsync();
        ravi.TeamId = east.Id; divya.TeamId = east.Id; arjun.TeamId = west.Id;
        await db.SaveChangesAsync();

        await SampleDataAsync(db, admin.Id, new[] { ravi.Id, divya.Id, arjun.Id, meera.Id });
        log.LogInformation("Demo data created");
    }

    private static async Task SampleDataAsync(CrmDbContext db, string adminId, string[] owners)
    {
        var rng = new Random(7);
        var today = DateTime.Today;

        var people = new (string Name, string Company, string City, string State)[]
        {
            ("Nikhil Reddy", "Reddy Cements", "Hyderabad", "Telangana"),
            ("Shreya Ghosh", "Ghosh Textiles", "Kolkata", "West Bengal"),
            ("Aditya Kulkarni", "Kulkarni Motors", "Pune", "Maharashtra"),
            ("Fatima Sheikh", "Crescent Foods", "Lucknow", "Uttar Pradesh"),
            ("Varun Menon", "Menon Logistics", "Kochi", "Kerala"),
            ("Ishita Jain", "Jain Pharma", "Indore", "Madhya Pradesh"),
            ("Rohit Saxena", "Saxena Steel", "Kanpur", "Uttar Pradesh"),
            ("Lavanya Pillai", "Coastal Resorts", "Goa", "Goa"),
            ("Harpreet Gill", "Gill Agro", "Ludhiana", "Punjab"),
            ("Ananya Bose", "Bose Interiors", "Bengaluru", "Karnataka"),
        };

        var customers = new List<Customer>();
        for (var i = 0; i < people.Length; i++)
        {
            var p = people[i];
            var first = p.Name.Split(' ')[0].ToLower();
            var c = Customer.Create($"CUST-{i + 1:D4}",
                new CustomerDetails(p.Name, $"{first}@{p.Company.Split(' ')[0].ToLower()}.in", $"9{870000000 + i * 1373}", p.Company,
                    $"{12 + i} MG Road", p.City, p.State, i == 7 ? CustomerStatus.Prospect : CustomerStatus.Active, null),
                owners[i % owners.Length]);
            c.MarkCreated(adminId, today.AddDays(-rng.Next(10, 300)));
            customers.Add(c);
        }
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        var prospects = new[] { "Pooja Rao", "Sandeep Yadav", "Neha Kapoor", "Imran Qureshi", "Tanvi Shah", "Gautam Sethi", "Ritu Arora", "Mohan Pillai", "Sneha Patil", "Kabir Malhotra", "Aisha Siddiqui", "Deepak Chawla" };
        var startStatus = new[] { LeadStatus.New, LeadStatus.Contacted };
        for (var i = 0; i < prospects.Length; i++)
        {
            var name = prospects[i];
            var lead = Lead.Create($"LEAD-{i + 1:D4}",
                new LeadDetails(name, $"{name.Split(' ')[0].ToLower()}@example.in", $"8{120000000 + i * 977}", $"{name.Split(' ')[1]} Traders",
                    (LeadSource)(i % 8), (LeadPriority)(i % 3), 40000 + rng.Next(1, 30) * 10000, null),
                startStatus[i % 2], owners[i % owners.Length]);
            if (i % 4 == 1) lead.MoveTo(LeadStatus.Qualified);
            if (i % 6 == 5) lead.MoveTo(LeadStatus.Lost);
            lead.MarkCreated(adminId, today.AddDays(-rng.Next(1, 120)));
            db.Leads.Add(lead);
        }

        var products = new[] { "CRM rollout", "Annual maintenance", "Fleet tracking", "Data migration", "POS terminals" };
        var stages = new[] { Stage.Qualification, Stage.Proposal, Stage.Negotiation, Stage.Won, Stage.Lost, Stage.Won, Stage.Proposal, Stage.Won, Stage.Negotiation, Stage.Qualification, Stage.Won, Stage.Lost, Stage.Won, Stage.Proposal };
        for (var i = 0; i < stages.Length; i++)
        {
            var c = customers[i % customers.Count];
            var created = today.AddDays(-rng.Next(40, 330));
            var closing = stages[i] is Stage.Won or Stage.Lost;
            var due = closing ? created.AddDays(rng.Next(20, 35)) : today.AddDays(rng.Next(7, 80));
            var details = new OpportunityDetails($"{c.Company} - {products[i % products.Length]}", c.Id, 150000 + rng.Next(1, 40) * 25000,
                stages[i] switch { Stage.Proposal => 40, Stage.Negotiation => 70, _ => 20 }, due, (LeadSource)(i % 8), null);

            var deal = Opportunity.Open(details, closing ? Stage.Negotiation : stages[i], c.OwnerId, closing ? created : today);
            if (closing) deal.Edit(details, stages[i], due, due);
            deal.MarkCreated(adminId, created);
            db.Opportunities.Add(deal);
        }
        await db.SaveChangesAsync();

        var subjects = new[] { "Demo call", "Price discussion", "Send revised quote", "Site visit", "Contract follow-up" };
        for (var i = 0; i < 12; i++)
        {
            var c = customers[i % customers.Count];
            // a few are set up as overdue by scheduling them "as of" an earlier day
            var asOf = i < 3 ? today.AddDays(-5) : today;
            var due = i < 3 ? today.AddDays(-rng.Next(1, 4)).AddHours(11) : today.AddDays(rng.Next(0, 15)).AddHours(10 + i % 5);
            var f = FollowUp.Schedule(TargetKind.Customer, c.Id, due, subjects[i % subjects.Length], (ContactType)(i % 4), null, c.OwnerId, asOf);
            if (i == 3) f.Complete("Customer agreed to a demo next week", today.AddHours(-3));
            f.MarkCreated(adminId, asOf.AddDays(-2));
            db.FollowUps.Add(f);
        }

        for (var i = 0; i < 15; i++)
        {
            var c = customers[i % customers.Count];
            var a = Activity.Log(new ActivityDetails((ContactType)(i % 4), $"{subjects[i % subjects.Length]} with {c.Name.Split(' ')[0]}", null,
                today.AddDays(-rng.Next(0, 45)).AddHours(9 + i % 7), c.Id, null, i % 5 == 0 ? ActivityStatus.Open : ActivityStatus.Done), c.OwnerId);
            a.MarkCreated(adminId, a.When);
            db.Activities.Add(a);
        }
        await db.SaveChangesAsync();
    }
}
