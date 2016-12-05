using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RestaurantApp
{
    public static class Dish
    {
        public static string Pizza = "pizza";
        public static string Piwo = "piwo";
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            var order = new Order();
            var manager = new Manager();
            var casier = new Casier(manager);
            var chef = new Chef(casier, "chef 1", 1000);
            var chef2 = new Chef(casier, "chef 2", 500);
            var chef3 = new Chef(casier, "chef 3", 250);
            ITimeToLeaveHandler timeToLeaveHandler = new TimeToLeaveHandler(chef);
            ITimeToLeaveHandler timeToLeaveHandler2 = new TimeToLeaveHandler(chef2);
            ITimeToLeaveHandler timeToLeaveHandler3 = new TimeToLeaveHandler(chef3);

            IQueueHandler handler = new QueueHandler(timeToLeaveHandler);
            IQueueHandler handler2 = new QueueHandler(timeToLeaveHandler2);
            IQueueHandler handler3 = new QueueHandler(timeToLeaveHandler3);
            Task.Factory.StartNew( ()=>PrintQueueSizes(handler, handler2, handler3));
            var kuchnia = new Kitchen(handler, handler2, handler3);

            var waiter = new Waiter(kuchnia);
            //ProccessManager proccessManager = new  

            foreach (var i in Enumerable.Range(1, 250))
            {
                order.MaxTimeOfWait = 10000;
                waiter.Handle(order);
            }

            while(true)
            {
                Thread.Sleep(1000);
            }
        }

        private static void PrintQueueSizes(IQueueHandler handler, IQueueHandler handler2, IQueueHandler handler3)
        {
            while (true)
            {
                Thread.Sleep(10);
                Console.Clear();
                Console.WriteLine($"Queue 1: {handler.QueueSize()}");
                Console.WriteLine($"Queue 2: {handler2.QueueSize()}");
                Console.WriteLine($"Queue 3: {handler3.QueueSize()}");
            }
        }
    }
    public class TimeToLeaveHandler : ITimeToLeaveHandler
    {
        private readonly Chef _chef;
        public TimeToLeaveHandler(Chef chef)
        {
            _chef = chef;
        }
        
        public Order Handle(Order order)
        {
            return _chef.Handle(order);
        }
    }

    public interface ITimeToLeaveHandler: IOrderHandler
    {
    }

    public interface IQueueHandler : IOrderHandler
    {
        void Start();
        int QueueSize();
    }

    public class QueueHandler : IQueueHandler
    {
        private readonly IOrderHandler _orderHandler;
        private readonly ConcurrentQueue<Order> _queue = new ConcurrentQueue<Order>();
        private object lockObj = new object();

        public QueueHandler(IOrderHandler orderHandler)
        {
            _orderHandler = orderHandler;
        }

        public Order Handle(Order order)
        {
            _queue.Enqueue(order);
            return order;
        }

        public void Start()
        {
            while (true)
            {
                Order order;
                if (_queue.TryDequeue(out order))
                    _orderHandler.Handle(order);
            }
        }

        public int QueueSize()
        {
            lock (lockObj)
            {
                return _queue.Count;
            }
        }
    }

    public class Kitchen : IOrderHandler
    {
        private readonly IQueueHandler[] _queues;
        private int _orderNumber;

        public Kitchen(params IQueueHandler[] queues)
        {
            _queues = queues;
            foreach (var queue in _queues)
            {
                queue.Start();
            }
        }

        public int MaxItemsInProccess = 100;
        public Order Handle(Order order)
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    foreach (var chefQueue in _queues)
                    {
                        if (chefQueue.QueueSize() < MaxItemsInProccess)
                        {
                            return chefQueue.Handle(order);
                        }
                    }
                }
            });
            

            return order;
        }
    }

    public class Casier : IOrderHandler
    {
        private readonly IOrderHandler _orderHandler;

        public Casier(IOrderHandler orderHandler)
        {
            _orderHandler = orderHandler;
        }

        public Order Handle(Order order)
        {
            order.Bill = SumUp(order);
            return _orderHandler.Handle(order);
        }

        private float SumUp(Order order)
        {
            return order.OrderedItems.Sum(s => s.Length);
        }
    }

    public class Manager : IOrderHandler
    {
        public Order Handle(Order order)
        {
            //Print(order);
            return order;
        }

        private void Print(Order order)
        {
            Console.WriteLine($"Order for table {order.TableNumber} cost {order.Bill}");
            Console.WriteLine($"Served dishes {string.Join(",", order.OrderedItems)}");
            Console.WriteLine($"Used ingredients {string.Join(",", order.Ingredients)}");
            Console.WriteLine($"Dish was prepared by {order.PreparedBy}");
        }
    }

    public interface IOrderHandler
    {
        Order Handle(Order order);
    }

    public class Order
    {
        public int TableNumber { get; set; }
        public string[] OrderedItems { get; set; }
        public IEnumerable<string> Ingredients { get; set; }
        public float Bill { get; set; }
        public string PreparedBy { get; set; }
        public int MaxTimeOfWait { get; set; }
        public bool IsRejected { get; set; }
    }

    public class Chef : IOrderHandler
    {
        private readonly IOrderHandler _orderHandler;
        private readonly string _name;
        public readonly int Lazzines;

        public Chef(IOrderHandler orderHandler, string name, int i)
        {
            _name = name;
            _orderHandler = orderHandler;
            Lazzines = i;
        }

        public Order Handle(Order order)
        {
            Thread.Sleep(Lazzines);
            order.Ingredients = GetIngredients(order.OrderedItems);
            order.PreparedBy = _name;
            return _orderHandler.Handle(order);
        }

        private IEnumerable<string> GetIngredients(string[] orderOrderedItems)
        {
            return orderOrderedItems.SelectMany(item => DishesReciepts[item]);
        }

        private IDictionary<string, string[]> DishesReciepts => new Dictionary<string, string[]>
        {
            {Dish.Pizza, new[] {"cheese", "ketchup", "base"}},
            {Dish.Piwo, new[] {"piwo"}}
        };
    }

    public class Waiter : IOrderHandler
    {
        private readonly IOrderHandler _handler;

        public Waiter(IOrderHandler handler)
        {
            _handler = handler;
        }

        public Order Handle(Order order)
        {
            order.TableNumber = 1;
            order.OrderedItems = new[] {Dish.Pizza, Dish.Piwo};
            return _handler.Handle(order);
        }
    }
}
