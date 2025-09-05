namespace DynamicConfiguration.Messaging
{
    /// <summary>RabbitMQ bağlantı ayarları.</summary>
    public class RabbitMQSettings
    {
        /// <summary>Host adı</summary>
        public string HostName { get; set; } = "localhost";

        /// <summary>Port numarası</summary>
        public int Port { get; set; } = 5672;

        /// <summary>Kullanıcı adı</summary>
        public string UserName { get; set; } = "guest";

        /// <summary>Şifre</summary>
        public string Password { get; set; } = "guest";

        /// <summary>Sanal host</summary>
        public string VirtualHost { get; set; } = "/";

        /// <summary>Exchange adı</summary>
        public string ExchangeName { get; set; } = "configuration.changes";

        /// <summary>Queue adı öneki</summary>
        public string QueueNamePrefix { get; set; } = "configuration.changes";

        /// <summary>Bağlantı timeout</summary>
        public int ConnectionTimeout { get; set; } = 30000;

        /// <summary>Heartbeat aralığı</summary>
        public ushort RequestedHeartbeat { get; set; } = 60;

        /// <summary>Otomatik kurtarma</summary>
        public bool AutomaticRecoveryEnabled { get; set; } = true;

        /// <summary>Ağ kurtarma aralığı</summary>
        public int NetworkRecoveryInterval { get; set; } = 5;
    }
}
