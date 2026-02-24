using System.Net;
using System.Net.Mail;

namespace BelekCommunity.Api.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void SendVerificationCode(string toEmail, string code)
        {
            // 1. Ayarları appsettings.json dosyasından çekiyoruz
            var smtpSettings = _configuration.GetSection("SmtpSettings");

            var host = smtpSettings["Host"] ?? "smtp.gmail.com";
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var senderEmail = smtpSettings["SenderEmail"];
            var password = smtpSettings["Password"];

            // Ayarlar eksikse hata fırlatmayalım, loglayalım veya varsayılan değer verelim
            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(password))
            {
                throw new Exception("SMTP ayarları (SenderEmail veya Password) appsettings.json dosyasında eksik.");
            }

            // 2. Mail Mesajını Oluşturuyoruz
            var mailMessage = new MailMessage
            {
                // DİKKAT: İkinci parametre ("Belek Üniversitesi...") alıcının göreceği isimdir.
                From = new MailAddress(senderEmail, "Belek Üniversitesi Topluluk Yönetimi"),
                Subject = "Belek Üniversitesi - Kayıt Doğrulama Kodu",

                // HTML Tasarımı
                Body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px; max-width: 600px; margin: auto;'>
                        <h2 style='color: #004085; text-align: center;'>Belek Üniversitesi Topluluk Platformu</h2>
                        <hr style='border: 0; border-top: 1px solid #eee;'>
                        <p style='font-size: 16px; color: #333;'>Merhaba,</p>
                        <p style='font-size: 14px; color: #555;'>
                            Topluluk yönetim sistemine kayıt işleminizi tamamlamak için doğrulama kodunuz aşağıdadır:
                        </p>
                        <div style='background-color: #f8f9fa; padding: 15px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                            <h1 style='margin: 0; letter-spacing: 5px; color: #0056b3; font-size: 32px;'>{code}</h1>
                        </div>
                        <p style='font-size: 14px; color: #555;'>
                            Bu kod <strong>3 dakika</strong> süreyle geçerlidir. Güvenliğiniz için bu kodu kimseyle paylaşmayınız.
                        </p>
                        <br>
                        <p style='font-size: 12px; color: #999; text-align: center;'>
                            © {DateTime.Now.Year} Belek Üniversitesi - Bilgi İşlem Daire Başkanlığı<br>
                            Bu mesaj otomatik olarak oluşturulmuştur, lütfen cevaplamayınız.
                        </p>
                    </div>
                ",
                IsBodyHtml = true, // HTML formatında olduğunu belirtiyoruz
            };

            mailMessage.To.Add(toEmail);

            // 3. SMTP İstemcisi Ayarları ve Gönderim
            using var smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true, // Gmail için SSL şarttır
            };

            smtpClient.Send(mailMessage);
        }
        public void SendEventCancellationEmail(string toEmail, string eventTitle, string communityName)
        {
            var smtpSettings = _configuration.GetSection("SmtpSettings");
            var host = smtpSettings["Host"] ?? "smtp.gmail.com";
            var port = int.Parse(smtpSettings["Port"] ?? "587");
            var senderEmail = smtpSettings["SenderEmail"];
            var password = smtpSettings["Password"];

            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(password)) return;

            var mailMessage = new MailMessage
            {
                From = new MailAddress(senderEmail, "Belek Üniversitesi Topluluk Yönetimi"),
                Subject = "Etkinlik İptali Bilgilendirmesi",
                Body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #ddd; border-radius: 5px; max-width: 600px; margin: auto;'>
                        <h2 style='color: #dc3545; text-align: center;'>Etkinlik İptal Edildi</h2>
                        <hr style='border: 0; border-top: 1px solid #eee;'>
                        <p style='font-size: 16px; color: #333;'>Merhaba,</p>
                        <p style='font-size: 14px; color: #555;'>
                            Kayıtlı olduğunuz <strong>{communityName}</strong> topluluğu tarafından düzenlenmesi planlanan <strong>{eventTitle}</strong> etkinliği yöneticiler tarafından iptal edilmiştir.
                        </p>
                        <p style='font-size: 14px; color: #555;'>Anlayışınız için teşekkür ederiz.</p>
                        <br>
                        <p style='font-size: 12px; color: #999; text-align: center;'>
                            © {DateTime.Now.Year} Belek Üniversitesi - Bilgi İşlem Daire Başkanlığı<br>
                            Bu mesaj otomatik olarak oluşturulmuştur.
                        </p>
                    </div>
                ",
                IsBodyHtml = true,
            };

            mailMessage.To.Add(toEmail);

            using var smtpClient = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = true,
            };

            smtpClient.Send(mailMessage);
        }
    }
}