namespace SharedData.Models
{
    public class ExpenseSplit
    {
        public int ExpenseId { get; set; }
        public Expense? Expense { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public decimal ShareAmount { get; set; }
    }
}
