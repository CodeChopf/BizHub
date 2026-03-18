using Microsoft.Data.Sqlite;

namespace AuraPrintsApi.Data;

public class DatabaseSeeder
{
    private readonly DatabaseContext _context;

    public DatabaseSeeder(DatabaseContext context)
    {
        _context = context;
    }

    public void Seed()
    {
        using var con = _context.CreateConnection();
        con.Open();

        using var check = con.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM weeks";
        var count = (long)(check.ExecuteScalar() ?? 0L);
        if (count > 0) return;

        using var tx = con.BeginTransaction();
        SeedWeeks(con);
        SeedTasks(con);
        SeedProducts(con);
        SeedCalculations(con);
        SeedPhase2(con);
        SeedLegal(con);
        SeedCategories(con);
        tx.Commit();
    }

    private void SeedWeeks(SqliteConnection con)
    {
        var weeks = new[]
        {
            (1, "Produktpalette & Prototypen", "Phase 1 — Fundament", "4h PC", "2h Drucker", (string?)null),
            (2, "Preiskalkulation & Verpackungskonzept", "Phase 1 — Fundament", "3h PC", "3h Drucker", (string?)null),
            (3, "Produktfotos — Shooting", "Phase 2 — Content & Shop", "1h PC", "5h Physisch", (string?)null),
            (4, "Etsy-Listings finalisieren & SEO", "Phase 2 — Content & Shop", "5h PC", "1h Drucker", (string?)null),
            (5, "Social Media Content vorbereiten", "Phase 2 — Content & Shop", "5h PC", "1h Physisch", (string?)null),
            (6, "Launch — Shop live schalten", "Phase 3 — Launch", "4h PC", "2h Physisch", "Erste Bewertungen sind Gold wert. 2–3 positive Reviews in den ersten Wochen verändern den Etsy-Algorithmus massgeblich zu deinen Gunsten."),
            (7, "Optimierung & Topseller identifizieren", "Phase 3 — Launch", "5h PC", "1h Drucker", (string?)null),
            (8, "Review & Phase 2 planen", "Phase 3 — Launch", "4h PC", "2h Drucker", "Milestone: Nach Woche 8 weisst du ob das Modell funktioniert. Erst dann in eigenen Shop, weitere Produkte oder Marketing-Budget investieren — nicht vorher.")
        };

        foreach (var (num, title, phase, bpc, bphys, note) in weeks)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO weeks (number, title, phase, badge_pc, badge_phys, note) VALUES (@n,@t,@p,@bp,@bph,@no)";
            cmd.Parameters.AddWithValue("@n", num);
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@p", phase);
            cmd.Parameters.AddWithValue("@bp", bpc);
            cmd.Parameters.AddWithValue("@bph", bphys);
            cmd.Parameters.AddWithValue("@no", (object?)note ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedTasks(SqliteConnection con)
    {
        var tasks = new[]
        {
            (1,1,"pc","Sortiment final festlegen: A–Z, 3 Grössen (S/M/L), 2 Farben. Ein einziges Etsy-Listing mit Varianten-Dropdown pro Buchstabentyp — spart Listing-Gebühren (CHF 0.21 pro Listing).","1h"),
            (1,2,"pc","Listing-Entwürfe auf Etsy überarbeiten: Varianten (Farbe, Grösse, Schriftart) im Dropdown einrichten. Lieferzeit '3–5 Werktage' eintragen — verhindert negative Bewertungen wegen Wartezeit.","2h"),
            (1,3,"phys","Prototypen aller 3 Grössen und 2 Farben drucken. Qualitätskontrolle: Layer-Qualität, Oberfläche, Standfestigkeit. Mind. 1 Exemplar pro Variante für Foto-Shooting in Woche 3.","2h"),
            (1,4,"pc","Versandprofil auf Etsy einrichten: Gewicht & Masse der Verpackung hinterlegen, Versandpreis für CH-Inland festlegen. Versandkosten auf Kunden überwälzen — im Listing kommunizieren.","1h"),
            (2,1,"pc","Finale Preise kalkulieren: Filament + Strom + Verpackung + Etsy-Gebühren (6.5% Transaktion + CHF 0.21 Listing). Wandbuchstabe ab CHF 4, Aufsteller ab CHF 8.","1h"),
            (2,2,"phys","Post-Processing testen: Schleifen, ggf. Grundierung oder Finishing. Standard festlegen — konsistente Oberfläche ist der sichtbarste Qualitätsunterschied zur Billigkonkurrenz.","2h"),
            (2,3,"phys","Verpackungskonzept finalisieren: schützende Verpackung, Beilagenblatt mit AuraPrints-Logo und Pflege-Hinweis. Material bestellen falls nicht vorhanden — Vorlaufzeit einrechnen.","1h"),
            (2,4,"pc","Shop-Policies auf Etsy schreiben: Rückgabe (keine Pflicht bei Einzelanfertigungen in CH), Versandzeiten, FAQ. Auf Deutsch, kurz und sachlich.","2h"),
            (3,1,"phys","Shooting-Setup aufbauen: weisser oder hellgrauer Hintergrund, natürliches Seitenlicht (Nordfenster am Morgen, kein direktes Sonnenlicht). Props: Holz, Beton oder Marmor für Lifestyle-Shots.","1h"),
            (3,2,"phys","Alle Produkte fotografieren: 1 Hauptbild weisser BG, 3–4 Lifestyle-Shots, mind. 1 Grössenreferenz (Hand, Münze oder Massband). Jede Farbe separat fotografieren.","3h"),
            (3,3,"pc","Fotos sichten, beste auswählen, leicht nachbearbeiten: Helligkeit, Weissabgleich. Kein Heavy Editing — Produkt soll realistisch wirken.","1h"),
            (3,4,"phys","Prozess-Content drehen: Drucker-Video, Timelapse, Behind-the-Scenes Verpackung. Rohmaterial für 4–5 Social Media Posts.","1h"),
            (4,1,"pc","Fotos in alle Listing-Entwürfe hochladen: Hauptbild weisser BG, dann Lifestyle, Grössenvergleich, Oberflächendetail. Stärkstes Bild immer zuerst.","1h"),
            (4,2,"pc","Titel SEO-optimieren: z.B. '3D Buchstaben Deko Schweiz Wohnzimmer'. Alle 13 Tags vollständig nutzen. Erank (kostenlos) zur Keyword-Recherche.","1.5h"),
            (4,3,"pc","Beschreibungen schreiben: Grössen in cm, Material, Lieferzeit, Montagemöglichkeiten. Sachlich und direkt — Schweizer Käufer schätzen klare Produktinfos.","1.5h"),
            (4,4,"pc","Shop-Banner und 'Über uns'-Text finalisieren: 3–4 Sätze, persönlich und direkt. Logo als Shop-Icon.","1h"),
            (4,5,"phys","Testbestellung simulieren: vollständigen Ablauf prüfen — Drucken, Post-Processing, Verpacken. Zeiten notieren für spätere Optimierung.","1h"),
            (5,1,"phys","Zusätzlichen Content drehen: Drucker-Nahaufnahme, Behind-the-Scenes Verpackung, Vorher/Nachher. Rohmaterial für mind. 4 weitere Posts.","1h"),
            (5,2,"pc","10–12 Posts vorproduzieren: Reels, Karussell, Produkt-Posts. Konsistentes Raster — helle Töne, viel Weissraum, kein Text-Overload.","2h"),
            (5,3,"pc","Bio optimieren: Etsy-Link einbauen, klare Beschreibung '3D Deko | Schweiz | Handgefertigt', Logo als Profilbild.","0.5h"),
            (5,4,"pc","Content-Kalender für 4 Wochen post-Launch: 3x/Woche, Mix aus Produkt / Prozess / Behind the Scenes. 5–10 Schweizer Interior-Accounts identifizieren und folgen.","2.5h"),
            (6,1,"pc","Alle Listings von 'Entwurf' auf 'aktiv' stellen. Launch-Post auf Social Media mit Etsy-Link. Nur echte Käufe für Bewertungen — keine gefälschten Reviews (Etsy-AGB).","1h"),
            (6,2,"phys","Erste Bestellungen abarbeiten: Drucken, Post-Processing, Verpacken, Versenden. Zeitaufwand dokumentieren — Basis für Topseller-Lagerplanung in Woche 7.","2h"),
            (6,3,"pc","Etsy-Statistiken täglich 10 Minuten checken: Impressionen, Klicks, Conversion Rate. Was wird angeschaut aber nicht gekauft?","1h"),
            (6,4,"pc","Social Media: 3 Posts veröffentlichen. Bei jeder Bestellung Etsy-Nachricht mit Bitte um Bewertung — auf Etsy Standard und erwartet.","2h"),
            (7,1,"pc","Etsy-SEO vertiefen: Erank oder Marmalead nutzen. Welche Keywords bringen echten Traffic? Titel und Tags der schwächsten Listings anpassen.","2h"),
            (7,2,"pc","Hauptbilder der schwächsten Listings testen: alternatives Foto einsetzen. Hauptbild ist der wichtigste Conversion-Faktor auf Etsy.","1h"),
            (7,3,"pc","Erste Daten auswerten: welche Buchstaben, Grösse, Farbe läuft? Top 3–5 Varianten identifizieren. Lagerbestand aufbauen, Lieferzeit reduzieren.","1h"),
            (7,4,"phys","Topseller-Lagerbestand aufbauen: 5–10 Stück der meistverkauften Varianten drucken. Erst jetzt — datenbasiert, nicht auf Vermutung.","1h"),
            (7,5,"pc","Social Media: 3 Posts, Fokus auf meistgekauftes Produkt. User Generated Content teilen falls vorhanden — stärkster Social Proof.","1h"),
            (8,1,"pc","Vollständiges Review: Umsatz, meistgekaufte Produkte, Conversion Rate, Kundenfeedback, reale Versandkosten vs. Kalkulation.","1.5h"),
            (8,2,"phys","HueForge-Bilder: erste Prototypen drucken, Qualität beurteilen, Rahmung testen. Marktreife für Phase 2 prüfen.","2h"),
            (8,3,"pc","Phase 2 Roadmap skizzieren: Sets/Namen-Listings, HueForge-Bilder, Preisanpassungen. Neue 8-Wochen-Planung aufsetzen.","1.5h"),
            (8,4,"pc","Steuerliche Situation prüfen: wenn Umsatz gegen CHF 2'300 tendiert, Einzelunternehmen-Anmeldung prüfen. Bei Bedarf Steuerberater konsultieren.","1h")
        };

        foreach (var (weekNum, sort, type, text, hours) in tasks)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO tasks (week_number, sort_order, type, text, hours) VALUES (@w,@s,@t,@tx,@h)";
            cmd.Parameters.AddWithValue("@w", weekNum);
            cmd.Parameters.AddWithValue("@s", sort);
            cmd.Parameters.AddWithValue("@t", type);
            cmd.Parameters.AddWithValue("@tx", text);
            cmd.Parameters.AddWithValue("@h", hours);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedProducts(SqliteConnection con)
    {
        var products = new[] { ("AP-W", "Wandbuchstabe", "wand"), ("AP-A", "Aufstellbuchstabe", "aufstell") };
        foreach (var (sku, name, type) in products)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO products (sku, name, type) VALUES (@s,@n,@t)";
            cmd.Parameters.AddWithValue("@s", sku);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@t", type);
            cmd.ExecuteNonQuery();
        }

        var variants = new[]
        {
            (1,"S","5 cm","~10 min","CHF 4"), (1,"M","15 cm","~20 min","CHF 8"), (1,"L","25 cm","~35 min","CHF 12"),
            (2,"S","5 cm","~15 min","CHF 8"), (2,"M","15 cm","~30 min","CHF 12"), (2,"L","25 cm","~50 min","CHF 18")
        };

        foreach (var (pid, size, height, pt, price) in variants)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO product_variants (product_id, size, height, print_time, price) VALUES (@p,@s,@h,@pt,@pr)";
            cmd.Parameters.AddWithValue("@p", pid);
            cmd.Parameters.AddWithValue("@s", size);
            cmd.Parameters.AddWithValue("@h", height);
            cmd.Parameters.AddWithValue("@pt", pt);
            cmd.Parameters.AddWithValue("@pr", price);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedCalculations(SqliteConnection con)
    {
        var calcs = new[]
        {
            ("AP-W-S-001","Wandbuchstabe S","CHF 4","CHF 2.75"),
            ("AP-W-M-001","Wandbuchstabe M","CHF 8","CHF 5.99"),
            ("AP-A-L-001","Aufstellbuchstabe L","CHF 18","CHF 14.04")
        };

        foreach (var (sku, name, sp, profit) in calcs)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO calculations (sku, name, sale_price, profit) VALUES (@s,@n,@sp,@p)";
            cmd.Parameters.AddWithValue("@s", sku);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@sp", sp);
            cmd.Parameters.AddWithValue("@p", profit);
            cmd.ExecuteNonQuery();
        }

        var costItems = new[]
        {
            (1,1,"Filament ~5g","− CHF 0.13"), (1,2,"Strom 10min","− CHF 0.05"), (1,3,"Verpackung","− CHF 0.60"),
            (1,4,"Etsy Listing","− CHF 0.21"), (1,5,"Etsy 6.5%","− CHF 0.26"),
            (2,1,"Filament ~15g","− CHF 0.38"), (2,2,"Strom 20min","− CHF 0.10"), (2,3,"Verpackung","− CHF 0.80"),
            (2,4,"Etsy Listing","− CHF 0.21"), (2,5,"Etsy 6.5%","− CHF 0.52"),
            (3,1,"Filament ~45g","− CHF 1.13"), (3,2,"Strom 50min","− CHF 0.25"), (3,3,"Verpackung","− CHF 1.20"),
            (3,4,"Etsy Listing","− CHF 0.21"), (3,5,"Etsy 6.5%","− CHF 1.17")
        };

        foreach (var (calcId, sort, label, amount) in costItems)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO cost_items (calculation_id, sort_order, label, amount) VALUES (@c,@s,@l,@a)";
            cmd.Parameters.AddWithValue("@c", calcId);
            cmd.Parameters.AddWithValue("@s", sort);
            cmd.Parameters.AddWithValue("@l", label);
            cmd.Parameters.AddWithValue("@a", amount);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedPhase2(SqliteConnection con)
    {
        var phase2 = new[]
        {
            ("Phase 2 · Nach Launch","5er-Set Name / Wort","CHF 35–45","z.B. 'MALIN', 'HOME'. ~60% Marge. Hauptprodukt Phase 2."),
            ("Phase 2 · Nach Launch","8er-Set personalisiert","CHF 55–70","Individueller Text, Wunschfarbe. Premium-Tier."),
            ("Phase 2 · Entwicklung","HueForge-Bild (gerahmt)","CHF 60–120","Prototypen in Woche 8. Marktreife prüfen."),
            ("Phase 2 · Optional","Eigener Onlineshop","Ab CHF 2'300","Nur nach Erreichen der Umsatzschwelle.")
        };

        foreach (var (label, name, price, note) in phase2)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO phase2 (label, name, price, note) VALUES (@l,@n,@p,@no)";
            cmd.Parameters.AddWithValue("@l", label);
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@p", price);
            cmd.Parameters.AddWithValue("@no", note);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedLegal(SqliteConnection con)
    {
        var legal = new[]
        {
            "Unter CHF 2'300 Jahresumsatz: kein Gewerbeeintrag, keine MWST-Pflicht.",
            "Ab erstem Verkauf: Einnahmen in Steuererklärung deklarieren.",
            "Ab CHF 2'300: Einzelunternehmen prüfen. Spätestens Woche 8 entscheiden.",
            "Designs eigenerstellt: kein Lizenzproblem.",
            "Rückgaberecht: kein gesetzliches Widerrufsrecht bei Einzelanfertigungen in CH."
        };

        foreach (var text in legal)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO legal (text) VALUES (@t)";
            cmd.Parameters.AddWithValue("@t", text);
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedCategories(SqliteConnection con)
    {
        var categories = new[]
        {
            ("Marketing",  "#4f8ef7"),
            ("Equipment",  "#a78bfa"),
            ("Verpackung",  "#34c77b"),
            ("Entwicklung", "#f5a623"),
            ("Sonstiges",  "#9699a8")
        };

        foreach (var (name, color) in categories)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = "INSERT INTO categories (name, color) VALUES (@n, @c)";
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@c", color);
            cmd.ExecuteNonQuery();
        }
    }
}