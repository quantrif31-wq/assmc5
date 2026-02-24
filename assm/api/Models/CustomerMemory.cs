namespace lab4.Models
{
    public class CustomerMemory
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? Email { get; set; }
        public int? TempProductId { get; set; }
        public string? TempProductName { get; set; }

        public string? PaymentMethod { get; set; }
        public List<CartItemMemory> Cart { get; set; } = new();
        public ConversationStage Stage { get; set; } = ConversationStage.None;
    }
}
