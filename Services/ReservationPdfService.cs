using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ImperialVip.Models;

namespace ImperialVip.Services
{
    public class ReservationPdfService : IReservationPdfService
    {
        private const string BandColor = "#1B1B3A";

        private static Dictionary<string, Dictionary<string, string>> GetTranslations()
        {
            return new Dictionary<string, Dictionary<string, string>>
            {
                ["tr"] = new()
                {
                    ["ArrivalInfo"] = "Geliş Bilgileri",
                    ["ReturnInfo"] = "Dönüş Bilgileri",
                    ["FullName"] = "Adı Soyadı",
                    ["Phone"] = "Telefon",
                    ["Email"] = "E-posta",
                    ["PickupPoint"] = "Alış Noktası",
                    ["DropoffPoint"] = "Varış Noktası",
                    ["ArrivalDate"] = "Geliş Tarihi",
                    ["ArrivalFlight"] = "Geliş Uçuş Numarası",
                    ["Airline"] = "Havayolu Şirketi",
                    ["HotelName"] = "Otel Adı",
                    ["Passengers"] = "Yolcular",
                    ["VehicleType"] = "Araç Türü",
                    ["Price"] = "Fiyat",
                    ["Adults"] = "Yetişkin Sayısı",
                    ["Children"] = "Çocuk Sayısı",
                    ["ChildSeats"] = "Çocuk Koltuğu Sayısı",
                    ["SpecialNote"] = "Özel Not",
                    ["ReturnDate"] = "Dönüş Tarihi",
                    ["ReturnFlight"] = "Dönüş Uçuş Numarası",
                    ["PickupTime"] = "Araç Alış Saati"
                },
                ["en"] = new()
                {
                    ["ArrivalInfo"] = "Arrival Information",
                    ["ReturnInfo"] = "Return Information",
                    ["FullName"] = "Full Name",
                    ["Phone"] = "Phone",
                    ["Email"] = "Email",
                    ["PickupPoint"] = "Pick-up Point",
                    ["DropoffPoint"] = "Drop-off Point",
                    ["ArrivalDate"] = "Arrival Date",
                    ["ArrivalFlight"] = "Arrival Flight Number",
                    ["Airline"] = "Airline",
                    ["HotelName"] = "Hotel Name",
                    ["Passengers"] = "Passengers",
                    ["VehicleType"] = "Vehicle Type",
                    ["Price"] = "Price",
                    ["Adults"] = "Number of Adults",
                    ["Children"] = "Number of Children",
                    ["ChildSeats"] = "Child Seats",
                    ["SpecialNote"] = "Special Note",
                    ["ReturnDate"] = "Return Date",
                    ["ReturnFlight"] = "Return Flight Number",
                    ["PickupTime"] = "Pick-up Time"
                },
                ["de"] = new()
                {
                    ["ArrivalInfo"] = "Ankunftsinformationen",
                    ["ReturnInfo"] = "Rückreiseinformationen",
                    ["FullName"] = "Vollständiger Name",
                    ["Phone"] = "Telefon",
                    ["Email"] = "E-Mail",
                    ["PickupPoint"] = "Abholort",
                    ["DropoffPoint"] = "Zielort",
                    ["ArrivalDate"] = "Ankunftsdatum",
                    ["ArrivalFlight"] = "Ankunftsflugnummer",
                    ["Airline"] = "Fluggesellschaft",
                    ["HotelName"] = "Hotelname",
                    ["Passengers"] = "Passagiere",
                    ["VehicleType"] = "Fahrzeugtyp",
                    ["Price"] = "Preis",
                    ["Adults"] = "Anzahl Erwachsene",
                    ["Children"] = "Anzahl Kinder",
                    ["ChildSeats"] = "Kindersitze",
                    ["SpecialNote"] = "Besondere Hinweise",
                    ["ReturnDate"] = "Rückreisedatum",
                    ["ReturnFlight"] = "Rückflugnummer",
                    ["PickupTime"] = "Abholzeit"
                },
                ["ru"] = new()
                {
                    ["ArrivalInfo"] = "Информация о прибытии",
                    ["ReturnInfo"] = "Информация об обратном трансфере",
                    ["FullName"] = "ФИО",
                    ["Phone"] = "Телефон",
                    ["Email"] = "Эл. почта",
                    ["PickupPoint"] = "Место посадки",
                    ["DropoffPoint"] = "Место высадки",
                    ["ArrivalDate"] = "Дата прибытия",
                    ["ArrivalFlight"] = "Номер рейса прибытия",
                    ["Airline"] = "Авиакомпания",
                    ["HotelName"] = "Название отеля",
                    ["Passengers"] = "Пассажиры",
                    ["VehicleType"] = "Тип автомобиля",
                    ["Price"] = "Цена",
                    ["Adults"] = "Взрослых",
                    ["Children"] = "Детей",
                    ["ChildSeats"] = "Детских кресел",
                    ["SpecialNote"] = "Особые пожелания",
                    ["ReturnDate"] = "Дата обратного трансфера",
                    ["ReturnFlight"] = "Номер обратного рейса",
                    ["PickupTime"] = "Время посадки"
                }
            };
        }

        public byte[] GenerateReservationPdf(Reservation reservation, string lang = "tr")
        {
            var translations = GetTranslations();
            var t = translations.ContainsKey(lang) ? translations[lang] : translations["tr"];

            var vehicleName = reservation.Vehicle?.Name ?? "-";
            var yolcular = string.IsNullOrEmpty(reservation.AdditionalPassengerNames)
                ? reservation.CustomerName
                : $"{reservation.CustomerName}, {reservation.AdditionalPassengerNames}";
            var currencySymbol = (reservation.Currency ?? "EUR").ToUpper() switch
            {
                "USD" => "$",
                "TRY" => "₺",
                "GBP" => "£",
                _ => "€"
            };
            var fiyat = reservation.EstimatedPrice.HasValue && reservation.EstimatedPrice.Value > 0
                ? $"{(int)reservation.EstimatedPrice.Value} {currencySymbol}"
                : $"0 {currencySymbol}";
            var yetiskin = reservation.NumberOfAdults ?? reservation.PassengerCount ?? 1;
            var cocuk = reservation.NumberOfChildren ?? 0;
            var cocukKoltuk = reservation.ChildSeatCount ?? 0;
            var ozelNot = string.IsNullOrWhiteSpace(reservation.Notes) ? "-" : reservation.Notes.Trim();
            var gelisTarihi = $"{reservation.TransferDate?.ToString("dd.MM.yyyy") ?? "-"} {reservation.TransferTime}";

            var isGidisDonus = (reservation.IsReturnTransfer == true) && reservation.ReturnTransferDate.HasValue;
            var donusTarihiStr = "";
            var donusUcusNo = reservation.ReturnFlightNumber ?? "-";
            var aracAlisSaati = reservation.ReturnTransferTime ?? "-";
            if (isGidisDonus)
            {
                donusTarihiStr = reservation.ReturnTransferDate!.Value.ToString("dd.MM.yyyy");
                if (!string.IsNullOrEmpty(reservation.ReturnTransferTime))
                    donusTarihiStr += " " + reservation.ReturnTransferTime;
            }

            var footerTel = isGidisDonus ? "+90 532 580 70 77" : "+90 533 925 10 20";

            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            byte[]? logoBytes = File.Exists(logoPath) ? File.ReadAllBytes(logoPath) : null;

            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    RenderHeader(page, logoBytes);

                    page.Content().PaddingHorizontal(30).PaddingTop(20).Column(col =>
                    {
                        col.Spacing(5);
                        col.Item().Text(t["ArrivalInfo"]).FontSize(14).Bold();
                        col.Item().PaddingTop(6);

                        Satir(col, t["FullName"], reservation.CustomerName);
                        Satir(col, t["Phone"], reservation.CustomerPhone);
                        Satir(col, t["Email"], reservation.CustomerEmail ?? "-");
                        Satir(col, t["PickupPoint"], reservation.PickupLocation);
                        Satir(col, t["DropoffPoint"], reservation.DropoffLocation);
                        Satir(col, t["ArrivalDate"], gelisTarihi);
                        Satir(col, t["ArrivalFlight"], reservation.FlightNumber ?? "-");
                        Satir(col, t["Airline"], reservation.AirlineCompany ?? "-");
                        Satir(col, t["HotelName"], reservation.HotelName ?? "-");
                        Satir(col, t["Passengers"], yolcular);
                        Satir(col, t["VehicleType"], vehicleName);
                        Satir(col, t["Price"], fiyat);
                        Satir(col, t["Adults"], yetiskin.ToString());
                        Satir(col, t["Children"], cocuk.ToString());
                        Satir(col, t["ChildSeats"], cocukKoltuk.ToString());
                        Satir(col, t["SpecialNote"], ozelNot);
                    });

                    RenderFooter(page, logoBytes, footerTel);
                });

                if (isGidisDonus)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                        RenderHeader(page, logoBytes);

                        page.Content().PaddingHorizontal(30).PaddingTop(20).Column(col =>
                        {
                            col.Spacing(5);
                            col.Item().Text(t["ReturnInfo"]).FontSize(14).Bold();
                            col.Item().PaddingTop(6);

                            Satir(col, t["ReturnDate"], donusTarihiStr);
                            Satir(col, t["ReturnFlight"], donusUcusNo);
                            Satir(col, t["PickupTime"], aracAlisSaati);
                        });

                        RenderFooter(page, logoBytes, "+90 532 580 70 77");
                    });
                }
            }).GeneratePdf(stream);

            return stream.ToArray();
        }

        private static void RenderHeader(PageDescriptor page, byte[]? logoBytes)
        {
            page.Header().Element(header =>
            {
                if (logoBytes != null)
                {
                    header.Background(BandColor).PaddingVertical(16).PaddingHorizontal(30)
                        .AlignCenter().Height(50).Image(logoBytes).FitHeight();
                }
                else
                {
                    header.Background(BandColor).Padding(20).AlignCenter()
                        .Text("IMPERIAL VIP").FontSize(22).Bold().FontColor(Colors.White);
                }
            });
        }

        private static void RenderFooter(PageDescriptor page, byte[]? logoBytes, string tel)
        {
            page.Footer().Element(footer =>
            {
                footer.Background(BandColor).PaddingVertical(12).PaddingHorizontal(30).Column(col =>
                {
                    col.Spacing(2);
                    if (logoBytes != null)
                        col.Item().AlignCenter().Height(28).Image(logoBytes).FitHeight();
                    col.Item().AlignCenter().Text("Imperial VIP Transfer - 2026")
                        .FontSize(10).FontColor(Colors.White);
                    col.Item().AlignCenter().Text($"info@transferimperialvip.com • {tel}")
                        .FontSize(9).FontColor(Colors.White);
                });
            });
        }

        private static void Satir(ColumnDescriptor col, string label, string value)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem(1).Text(label).Bold().FontSize(11);
                row.RelativeItem(2).Text(value).FontSize(11);
            });
        }
    }
}
