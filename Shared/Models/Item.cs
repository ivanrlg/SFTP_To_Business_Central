using System.Text.Json.Serialization;

namespace Shared.Models
{
    public class Item
    {
        public string No { get; set; }
        public string Description { get; set; }

        public string UnitOfMeasure { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal UnitCost { get; set; }
    }
}

