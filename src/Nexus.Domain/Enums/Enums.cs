namespace Nexus.Domain.Enums;

public enum ClientType { Individual = 1, Company = 2 }
public enum ClientStatus { Active = 1, Inactive = 2, Prospect = 3, Blocked = 4 }

public enum TicketStatus
{
    Open = 1, Analyzing = 2, InProgress = 3, WaitingClient = 4,
    Resolved = 5, Closed = 6, Cancelled = 7
}
public enum TicketPriority { Low = 1, Medium = 2, High = 3, Critical = 4 }
public enum TicketCategory
{
    Support = 1, Infrastructure = 2, Network = 3, Security = 4, Development = 5,
    Automation = 6, Hardware = 7, Software = 8, Consulting = 9, Other = 10
}

public enum TransactionType { Income = 1, Expense = 2 }
public enum TransactionStatus { Pending = 1, Paid = 2, Overdue = 3, Cancelled = 4 }
public enum RecurrenceType { None = 0, Weekly = 1, Monthly = 2, Quarterly = 3, Yearly = 4 }

public enum ServerType { VPS = 1, Dedicated = 2, HomeServer = 3, Container = 4, VirtualMachine = 5, Cloud = 6 }
public enum ServerStatus { Online = 1, Offline = 2, Maintenance = 3, Unknown = 4 }

public enum AlertSeverity { Info = 1, Warning = 2, Error = 3, Critical = 4 }
public enum AlertType { ServerDown = 1, TicketOverdue = 2, PaymentDue = 3, ClientWaiting = 4, SystemEvent = 5, Custom = 6 }

public enum DocumentStatus { Draft = 1, Sent = 2, Accepted = 3, Rejected = 4, Expired = 5 }

public enum WorkItemPriority { Low = 1, Medium = 2, High = 3, Urgent = 4 }
public enum WorkItemStatus { Todo = 1, InProgress = 2, Done = 3, Cancelled = 4 }
