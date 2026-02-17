using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ImperialVip.Models;

namespace ImperialVip.Services
{
    /// <summary>
    /// Rezervasyon_5358_tr.pdf ve Rezervasyon_5363_tr.pdf yapısını birebir kopyalar.
    /// </summary>
    public class ReservationPdfService : IReservationPdfService
    {
        public byte[] GenerateReservationPdf(Reservation reservation)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var vehicleName = reservation.Vehicle?.Name ?? "Belirtilmedi";
            var yolcular = string.IsNullOrEmpty(reservation.AdditionalPassengerNames)
                ? reservation.CustomerName
                : $"{reservation.CustomerName}, {reservation.AdditionalPassengerNames}";
            var fiyat = reservation.EstimatedPrice.HasValue && reservation.EstimatedPrice.Value > 0
                ? $"{(int)reservation.EstimatedPrice.Value} €"
                : "0 €";
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

            using var stream = new MemoryStream();
            var donusFooterTel = "+90 532 580 70 77";

            Document.Create(container =>
            {
                // Sayfa 1: Geliş Bilgileri
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    page.Header().Element(header =>
                    {
                        header.Background(Colors.Black).Padding(20).AlignCenter()
                            .Text("İmperial VİP Turizm")
                            .FontSize(22).Bold().FontColor(Colors.White).FontFamily("Arial");
                    });

                    page.Content().Padding(25).Column(col =>
                    {
                        col.Spacing(4);
                        col.Item().PaddingTop(10).Text("Geliş Bilgileri").FontSize(14).Bold();
                        col.Item().PaddingTop(8);

                        Satir(col, "Adı Soyadı", reservation.CustomerName);
                        Satir(col, "Telefon", reservation.CustomerPhone);
                        Satir(col, "E-posta", reservation.CustomerEmail ?? "-");
                        Satir(col, "Alış Noktası", reservation.PickupLocation);
                        Satir(col, "Varış Noktası", reservation.DropoffLocation);
                        Satir(col, "Geliş Tarihi", gelisTarihi);
                        Satir(col, "Geliş Uçuş Numarası", reservation.FlightNumber ?? "-");
                        Satir(col, "Havayolu Şirketi", reservation.AirlineCompany ?? "-");
                        Satir(col, "Otel Adı", reservation.HotelName ?? "-");
                        Satir(col, "Yolcular", yolcular);
                        Satir(col, "Araç Türü", vehicleName);
                        Satir(col, "Fiyat", fiyat);
                        Satir(col, "Yetişkin Sayısı", yetiskin.ToString());
                        Satir(col, "Çocuk Sayısı", cocuk.ToString());
                        Satir(col, "Çocuk Koltuğu Sayısı", cocukKoltuk.ToString());
                        Satir(col, "Özel Not", ozelNot);

                        col.Item().PaddingTop(16);
                        col.Item().Text("Imperial VIP Transfer - 2026").FontSize(11);
                        col.Item().Text($"info@transferimperialvip.com • {footerTel}").FontSize(10);
                        col.Item().PaddingTop(8);
                        col.Item().AlignCenter().Text(isGidisDonus ? "-- 1 of 2 --" : "-- 1 of 1 --").FontSize(10).FontColor(Colors.Grey.Medium);
                    });
                });

                // Sayfa 2: Dönüş Bilgileri (gidiş-dönüş varsa)
                if (isGidisDonus)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(0);
                        page.PageColor(Colors.White);
                        page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                        page.Header().Element(header =>
                        {
                            header.Background(Colors.Black).Padding(20).AlignCenter()
                                .Text("İmperial VİP Turizm")
                                .FontSize(22).Bold().FontColor(Colors.White).FontFamily("Arial");
                        });

                        page.Content().Padding(25).Column(col =>
                        {
                            col.Spacing(4);
                            col.Item().PaddingTop(10).Text("Dönüş Bilgileri").FontSize(14).Bold();
                            col.Item().PaddingTop(8);

                            Satir(col, "Dönüş Tarihi", donusTarihiStr);
                            Satir(col, "Dönüş Uçuş Numarası", donusUcusNo);
                            Satir(col, "Araç Alış Saati", aracAlisSaati);

                            col.Item().PaddingTop(16);
                            col.Item().Text("Imperial VIP Transfer - 2026").FontSize(11);
                            col.Item().Text($"info@transferimperialvip.com • {donusFooterTel}").FontSize(10);
                            col.Item().PaddingTop(8);
                            col.Item().AlignCenter().Text("-- 2 of 2 --").FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    });
                }
            }).GeneratePdf(stream);

            return stream.ToArray();
        }

        private static void Satir(ColumnDescriptor col, string label, string value)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Text(label).Bold().FontSize(11);
                row.RelativeItem(2).AlignRight().Text(value).FontSize(11);
            });
        }
    }
}
