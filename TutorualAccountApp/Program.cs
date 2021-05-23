using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace TutorualAccountApp
{
    class Program
    {
        static void Main(string[] args)
        {

            /*
             * 
             Это приложение для учета личных финансов.
             Оно делает следующее:
             1. добавить денежную операцию, 
             2. вывести список всех операций
             3. вывести баланс в указанной валюте (начальный баланс 5000 eur, базовая валюта = EUR)
             4. курсы валют пересчитываются по информации с https://exchangeratesapi.io/, запрос идет 1 раз при запуске программы
             5. добавлены типы операций income и transfer
             6. добавлен репозиторий, которы читает траназкции с диска

            поддерживается 3 валюты: rur, eur, usd 
            балансом в конкретной валюте считаем сумму операций в данной валюте

            */

            ITransactionParser parser = new TransactionParser();

            //ITransactionRepository repo = new InMemoryTransactionRepository();
            ITransactionRepository repo = new HDDTransactionRepository("transactions.txt", parser);
            ICurrencyConverter converter = new CurrencyConverter();



            //выход, если не удалось подключение к серверу
            if (!converter.converterIsOn) return;

            IBudgetApplication app = new BudgetApplication(repo, parser, converter);

            app.balance = 5000;

            ITransaction transaction;

            //добавляем семпловые транзакции, чтобы не руками
            app.AddTransaction( new Expense(new CurrencyAmount("rub", 300), DateTime.Now, "Кофе", "0011"));
            app.AddTransaction(new Expense(new CurrencyAmount("rub", 500), DateTime.Now, "Плюшки", "0011"));
            app.AddTransaction(new Expense(new CurrencyAmount("eur", 100), DateTime.Now, "Кофе", "0011"));
            app.AddTransaction(new Expense(new CurrencyAmount("eur", 400), DateTime.Now, "Кофе", "0011"));
            app.AddTransaction(new Expense(new CurrencyAmount("usd", 2000), DateTime.Now, "Кофе", "0011"));
            app.AddTransaction(new Expense(new CurrencyAmount("usd", 100), DateTime.Now, "Кофе", "0011"));

            Console.WriteLine("Welcome to financial app!");

            bool exitMarker;

            do 
            {
                exitMarker = false;

                Console.WriteLine("Pleace enter transaction like 'expense 200 rur Vacation Coffee', 'list', 'balance rur/eur/usd' or 'exit'");

                string input = Console.ReadLine();

                input = input.Trim().ToLower();

                string[] x = input.Split(' ');

                switch (x[0])
                {
                    case "exit":
                        exitMarker = true;
                        break;

                    case "list":
                        app.OutputTransactions();
                        break;

                    case "balance":
                        app.OutputBalanceInCurrency(x[1]);
                        break;

                    default:
                        transaction = parser.Parse(input);

                        if (transaction != null)
                        {
                            app.AddTransaction(transaction);
                        }
                        break;
                }

                if (exitMarker) break;

            }

            while (true);

            Console.WriteLine("Thax 4 using our financial app!");

        }
    }



    //интефейсы
    public interface IBudgetApplication
    {
        decimal balance { get; set; }
        void AddTransaction(string input);
        void AddTransaction(ITransaction transaction);
        void OutputTransactions();
        void OutputBalanceInCurrency(string currencyCode);
    }
    
    public interface ICurrencyAmount
    {
        string CurrencyCode { get; }
        decimal Amount { get; set; }
    }
        
    public interface ITransaction
    {
        DateTimeOffset Date { get; }
        ICurrencyAmount Amount { get; set; }

    }
       
    public interface ITransactionRepository
    {
        void AddTransaction(ITransaction transaction);
        ITransaction[] GetTransactions();

    }

    public interface ITransactionParser
    {
        ITransaction Parse(string input);
    }

    public interface ICurrencyConverter
    {
        ICurrencyAmount ConvertCurrency(ICurrencyAmount amount, string currencyCode);
        bool converterIsOn { get; set; }
    }

    //классы
    public class BudgetApplication : IBudgetApplication
    {
        public decimal balance { get; set; } = 0; //баланс в Евро
        
        ICurrencyAmount balanceObject(string currencyCode)
        {
            //возвлащает баланс в указанной валюте
            ICurrencyAmount x = new CurrencyAmount("EUR", balance);
            x = (currencyCode == "EUR") ? x : currencyConverter.ConvertCurrency(x, currencyCode);
            return x;
        }

        ITransactionRepository transactionRepository;
        ITransactionParser transactionParser;
        ICurrencyConverter currencyConverter;

        //применяем концепцию базовой валюты, считаем что она = rur
        //decimal balance = 20000;

        public BudgetApplication(ITransactionRepository _transactionRepository, ITransactionParser _transactionParser, ICurrencyConverter _currencyConverter)
        {
            transactionRepository = _transactionRepository;
            transactionParser = _transactionParser;
            currencyConverter = _currencyConverter;
        }

        public void AddTransaction(string input)
        {
            ITransaction transaction;
            transaction = transactionParser.Parse(input);
            transactionRepository.AddTransaction(transaction);
        }
        public void AddTransaction(ITransaction transaction)
        {
            transactionRepository.AddTransaction(transaction);
        }
        public void OutputTransactions()
        {
            ITransaction[] transactionsArr = transactionRepository.GetTransactions();

            Console.WriteLine("Вывод транзакций:");
            foreach (ITransaction t in transactionsArr)
            {
                Console.WriteLine(t.ToString());
            }
        }
        public void OutputBalanceInCurrency(string currencyCode)
        {
            var totalCurrencyAmount = new CurrencyAmount(currencyCode, 0);

            var amounts = transactionRepository.GetTransactions()
                .Select(t => t.Amount)
                .Select(a => a.CurrencyCode != currencyCode ? currencyConverter.ConvertCurrency(a, currencyCode) : a)
                .ToArray();

            ICurrencyAmount totalOperationsAmount = amounts.Aggregate(totalCurrencyAmount, (t, a) => t += a);

            ICurrencyAmount rez = (CurrencyAmount)balanceObject(currencyCode) + totalOperationsAmount;

            Console.WriteLine($"Balance: {Math.Round(rez.Amount, 2)} {currencyCode}");
            Console.WriteLine("");


        }

    }
    public class CurrencyAmount : ICurrencyAmount, IEquatable<CurrencyAmount>
    {
        public CurrencyAmount(string currencyCode, decimal amount)
        {
            CurrencyCode = currencyCode;
            Amount = amount;
        }

        public string CurrencyCode { get; }

        public decimal Amount { get; set; }

        public static CurrencyAmount operator +(CurrencyAmount x, ICurrencyAmount y)
        {
            if (x.CurrencyCode == y.CurrencyCode)
            {
                return new CurrencyAmount(x.CurrencyCode, x.Amount + y.Amount);
            }
            return null;
        }

        public static CurrencyAmount operator -(CurrencyAmount x, ICurrencyAmount y)
        {
            if (x.CurrencyCode == y.CurrencyCode)
            {
                return new CurrencyAmount(x.CurrencyCode, x.Amount - y.Amount);
            }
            return null;

        }

        public static bool operator ==(CurrencyAmount left, CurrencyAmount right)
        {
            return EqualityComparer<CurrencyAmount>.Default.Equals(left, right);
        }

        public static bool operator !=(CurrencyAmount left, CurrencyAmount right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            return $"{Amount:0.00} {CurrencyCode}";
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CurrencyAmount);
        }

        public bool Equals(CurrencyAmount other)
        {
            return other != null &&
                   CurrencyCode == other.CurrencyCode &&
                   Amount == other.Amount;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(CurrencyCode, Amount);
        }
    }
    public class TransactionParser : ITransactionParser
    {
        public ITransaction Parse(string input)
        {
            try
            {
                var date = DateTimeOffset.Now;
                var splits = input.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                var typeCode = splits[0];
                var currencyAmount = new CurrencyAmount(splits[2], decimal.Parse(splits[1]));
                switch (typeCode.ToLower())
                {
                    case "expense":
                        //return new Expense(currencyAmount, date, splits[3], splits[4]);
                        return new Expense(currencyAmount, date, splits[3], splits[4]);
                    case "transfer":
                        return new Transfer(currencyAmount, date, splits[3], splits[4]);
                    case "income":
                        return new Income(currencyAmount, date, splits[3], splits[4]);
                    default:
                        Console.WriteLine("Это не транзакция");
                        return null;
                }
            }
            catch 
            {
                Console.WriteLine("Ошибка обработки транзакции");
                return null;
            }

        }
    }
    public class InMemoryTransactionRepository: ITransactionRepository
    {
        List<ITransaction> items = new List<ITransaction>();

        void ITransactionRepository.AddTransaction(ITransaction transaction)
        {
            items.Add(transaction);
            Console.WriteLine("Транзакция добавлена: "+ transaction.ToString());
        }

        ITransaction[] ITransactionRepository.GetTransactions()
        {
            return items.ToArray();
        }
    }

    public class HDDTransactionRepository : ITransactionRepository
    {
        //реализация ITransactionRepository, которая читает трнанзакции с диска а потом просто хранит остальные в памяти
        string fileName;
        ITransactionParser parser;

        List<ITransaction> items = new List<ITransaction>();

        public HDDTransactionRepository(string _fileName, ITransactionParser _parser)
        {
            fileName = _fileName;
            parser = _parser;
            readTransactionsFromHdd();
        }

        void readTransactionsFromHdd()
        {
            try
            {
                Console.WriteLine("Чтение траназкций с диска:");
                string[] _lines = File.ReadAllLines(fileName);
                foreach (string x in _lines)
                {
                    ITransaction b = parser.Parse(x);

                    if (b != null) AddTransaction(b);
                }
            }
            catch
            {
                //просто пересоздаем items по новой
                Console.WriteLine("Ошибка чтения данных с диска");
            }
        }


        public void AddTransaction(ITransaction transaction)
        {
            items.Add(transaction);
            Console.WriteLine("Транзакция добавлена: " + transaction.ToString());
        }

        ITransaction[] ITransactionRepository.GetTransactions()
        {
            
            
            
            return items.ToArray();
        }
    }

    public abstract class TransactionPattern
    {
        //это трата на что-то
        public TransactionPattern(ICurrencyAmount _Amount, DateTimeOffset _Date, string _Category, string _Destination)
        {
            Date = _Date;
            Amount = _Amount;
            Category = _Category;
            Destination = _Destination;
        }

        public virtual string transactionType{get;}

        public DateTimeOffset Date { get; }

        public ICurrencyAmount Amount { get; set; }
        public string Category { get; }
        public string Destination { get; }

        public override string ToString()
        {
            return string.Format("{0} {1} {2} {3} {4} {5}", transactionType, Date.ToString(), Amount.Amount.ToString(), Amount.CurrencyCode, Category, Destination);
        }
    }
    public class Expense : TransactionPattern,  ITransaction
    {
        public Expense(ICurrencyAmount _Amount, DateTimeOffset _Date, string _Category, string _Destination) : base(_Amount, _Date, _Category, _Destination) 
        {
            Amount.Amount = Amount.Amount*-1;
        }
        public override string transactionType { get { return "Expense"; } }
    }
    public class Transfer : TransactionPattern,  ITransaction
    {
        public Transfer(ICurrencyAmount _Amount, DateTimeOffset _Date, string _Category, string _Destination) : base(_Amount, _Date, _Category, _Destination)
        {
            Amount.Amount = Amount.Amount * -1;
        }
        public override string transactionType { get { return "Transfer"; } }
    }
    public class Income : TransactionPattern, ITransaction
    {
        public Income(ICurrencyAmount _Amount, DateTimeOffset _Date, string _Category, string _Destination) : base(_Amount, _Date, _Category, _Destination) { }
        public override string transactionType { get { return "Income"; } }
    }



    public class CurrencyConverter : ICurrencyConverter
    {
        CuurencyRate curRate=null;
        public bool converterIsOn { get; set; } = false;

        public CurrencyConverter()
        {
            //здесь на конструкторе мы ходим на сервер с курсами валют и один раз на запуск достаем json 
            string jsonRezult = "";
            string url = string.Format(@"http://api.exchangeratesapi.io/v1/latest?access_key=726c50ab71840690c3c86cd496d78047");
            try
            {
                var request = WebRequest.Create(url);
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream))
                {
                    jsonRezult = reader.ReadToEnd();
                }
                curRate = CuurencyRate.FromJson(jsonRezult);
                converterIsOn = true;
            }
            catch
            {
                Console.WriteLine("Error initializing converter");
            }
        }

        public ICurrencyAmount ConvertCurrency(ICurrencyAmount amount, string currencyCode)
        {
            string oldCur = amount.CurrencyCode.ToUpper();
            string newCur = currencyCode.ToUpper();

            decimal oldCur2EUR = oldCur=="EUR" ? 1 : curRate.rates[oldCur];
            decimal newCur2EUR = newCur == "EUR" ? 1 : curRate.rates[newCur];

            //decimal k = oldCur2EUR/newCur2EUR; //переводной коэф.
            decimal k = newCur2EUR/ oldCur2EUR; //переводной коэф.
            ICurrencyAmount newAmount = new CurrencyAmount(currencyCode, amount.Amount * k);

            return newAmount;
        }

        public class CuurencyRate
        {
            [JsonProperty("success")]
            public bool success { get; set; }

            [JsonProperty("base")]
            public string base0 { get; set; }

            [JsonProperty("rates")]
            public Dictionary<string, decimal> rates { get; set; }

            public static CuurencyRate FromJson(string json) => JsonConvert.DeserializeObject<CuurencyRate>(json, QuickType.Converter.Settings);
        }

    }


}
