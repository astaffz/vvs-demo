using System.Net;
using Microsoft.Data.Sqlite;

// Baza u memoriji: kreira se pri svakom pokretanju, nestaje kad se aplikacija ugasi.
var baza = new SqliteConnection("Data Source=:memory:");
baza.Open();
Pripremi(baza);

var app = WebApplication.CreateBuilder(args).Build();

app.MapGet("/", () => Results.Content(Stranica(""), "text/html"));

app.MapPost("/", (HttpRequest req) =>
{
    string ime = req.Form["ime"].ToString();              // 1. izvor: unos s forme

    // "Zaštita" 1: ne dozvoli pretragu admina
    if (ime.Contains("admin", StringComparison.OrdinalIgnoreCase)) return Results.Content(Stranica(""), "text/html");

    string upit = NapraviUpit(ime);                        // 2. prolazi kroz metodu

    string rezultat;
    try { rezultat = Tabela(Izvrsi(baza, upit)); }         // 3. ...i kroz drugu
    catch (SqliteException e) { rezultat = $"<p><b>Greška baze:</b> {WebUtility.HtmlEncode(e.Message)}</p>"; }

    string odgovor = $"<p>Pozdrav, {ime}!</p>" +           // 5. ponor: XSS (nekodiran unos u HTML)
                     $"<p>Upit: <code>{WebUtility.HtmlEncode(upit)}</code></p>" + rezultat;
    return Results.Content(Stranica(odgovor), "text/html");
});

app.Run();

// "Zaštita" 2: upit nikad ne vraća administratore
static string NapraviUpit(string ime) =>
    "SELECT Ime, Email, Uloga FROM Korisnici WHERE Ime = '" + ime + "' AND Uloga <> 'administrator'";

static List<string[]> Izvrsi(SqliteConnection baza, string sql)
{
    using var cmd = baza.CreateCommand();
    cmd.CommandText = sql;                                 // 4. ponor: SQL injection
    using var r = cmd.ExecuteReader();
    var redovi = new List<string[]>();
    while (r.Read()) redovi.Add([r.GetString(0), r.GetString(1), r.GetString(2)]);
    return redovi;
}

static void Pripremi(SqliteConnection baza)
{
    using var cmd = baza.CreateCommand();
    cmd.CommandText = """
        CREATE TABLE Korisnici (Ime TEXT, Email TEXT, Uloga TEXT);
        INSERT INTO Korisnici VALUES
          ('Aid',     'aid@example.com',     'student'),
          ('Amina',   'amina@example.com',   'student'),
          ('Emina',   'emina@example.com',   'student'),
          ('Sarah',   'sarah@example.com',   'student'),
          ('Dženana', 'ddonko@etf.unsa.ba',  'asistent'),
          ('Admin',   'admin@utic.net.ba',   'administrator');
        """;
    cmd.ExecuteNonQuery();
}

static string Tabela(List<string[]> redovi)
{
    if (redovi.Count == 0) return "<p>Nema korisnika s tim imenom.</p>";
    var html = "<table><tr><th>Ime</th><th>Email</th><th>Uloga</th></tr>";
    foreach (var red in redovi)
        html += "<tr>" + string.Concat(red.Select(c => $"<td>{WebUtility.HtmlEncode(c)}</td>")) + "</tr>";
    return html + $"</table><p>{redovi.Count} red(ova)</p>";
}

static string Stranica(string sadrzaj) => $$"""
    <!doctype html>
    <html lang="bs">
    <head>
      <meta charset="utf-8"><title>VVS demo</title>
      <style>
        body { font-family: system-ui; max-width: 44rem; margin: 3rem auto; padding: 0 1rem }
        table { border-collapse: collapse; margin-top: .5rem }
        th, td { border-bottom: 1px solid #ccc; padding: .3rem .8rem; text-align: left }
        code { background: #f2eee6; padding: .1rem .3rem }
      </style>
    </head>
    <body>
      <h1>Pretraga korisnika</h1>
      <form method="post">
        <label>Ime: <input name="ime" autofocus></label>
        <button>Traži</button>
      </form>
      {{sadrzaj}}
    </body>
    </html>
    """;