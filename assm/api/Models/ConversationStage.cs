namespace lab4.Models
{
    public enum ConversationStage
    {
        None = 0,
        AskingQuantity = 1,
        AskingForMore = 2,
        ConfirmingOrder = 3,
        AskingPaymentMethod = 4,
        CollectingInfo = 5,
        Payment = 6,
        Completed = 7
    }
}
