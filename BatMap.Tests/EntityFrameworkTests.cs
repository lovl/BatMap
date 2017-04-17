﻿using System;
using System.Data.Entity;
using FizzWare.NBuilder;
using NUnit.Framework;
using BatMap.Tests.DTO;
using BatMap.Tests.Model;
using System.Linq;
using Moq;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace BatMap.Tests {

    [TestFixture]
    public class EntityFrameworkTests {
        private readonly IList<Order> _orders;

        public EntityFrameworkTests() {
            var random = new Random();

            var products = Builder<Product>
                .CreateListOfSize(15)
                .All()
                .Do(p => p.Supplier = Builder<Company>.CreateNew().Build())
                .Build();

            _orders = Builder<Order>
                .CreateListOfSize(10)
                .All()
                .Do(o => {
                    o.OrderDetails = Builder<OrderDetail>
                        .CreateListOfSize(3)
                        .All()
                        .Do(od => od.Product = products[random.Next(15)])
                        .Build();
                })
                .Build();
        }

        [Test]
        public void Project_Orders() {
            var config = new MapConfiguration();
            config.RegisterMap<Order, OrderDTO>();
            config.RegisterMap<OrderDetail, OrderDetailDTO>();
            config.RegisterMap<Product, ProductDTO>();

            var mockContext = new Mock<TestEntities>();
            var observableOrders = new ObservableCollection<Order>(_orders);
            mockContext.Setup(p => p.Orders).Returns(GetMockSet(observableOrders).Object);

            var query = mockContext.Object.Orders;
            var dtoQuery = config.ProjectTo<OrderDTO>(query);
            var dtoList = dtoQuery.ToList();

            Assert.IsNull(dtoList[3].OrderDetails);
        }

        [Test]
        public void Project_Orders_With_Details_Product_Company() {
            var config = new MapConfiguration(DynamicMapping.MapAndCache);
            
            var mockContext = new Mock<TestEntities>();
            var observableOrders = new ObservableCollection<Order>(_orders);
            mockContext.Setup(p => p.Orders).Returns(GetMockSet(observableOrders).Object);

            var query = mockContext.Object.Orders;
            var dtoQuery = config.ProjectTo<Order, OrderDTO>(query, o => o.OrderDetails.Select(od => od.Product.Supplier));
            var dtoList = dtoQuery.ToList();

            Assert.AreEqual(
                dtoList[3].OrderDetails.ToList()[2].Product.Supplier.CompanyName, 
                _orders[3].OrderDetails[2].Product.Supplier.CompanyName
            );
        }

        [Test]
        public void Project_Orders_With_Details_Product_Company_2() {
            var config = new MapConfiguration(DynamicMapping.MapAndCache);

            var mockContext = new Mock<TestEntities>();
            var observableOrders = new ObservableCollection<Order>(_orders);
            mockContext.Setup(p => p.Orders).Returns(GetMockSet(observableOrders).Object);

            var query = mockContext.Object.Orders;
            var dtoQuery = config.ProjectTo<Order, OrderDTO>(query, o => o.OrderDetails.Select(od => od.Product).Select(p => p.Supplier));
            var dtoList = dtoQuery.ToList();

            Assert.AreEqual(
                dtoList[3].OrderDetails.ToList()[2].Product.Supplier.CompanyName,
                _orders[3].OrderDetails[2].Product.Supplier.CompanyName
            );
        }

        [Test]
        public void Project_Orders_Custom_Expression() {
            var config = new MapConfiguration();
            config.RegisterMap<Order, OrderDTO>((o, mc) => new OrderDTO {
                Id = o.Id,
                OrderNo = o.OrderNo,
                OrderDetails = o.OrderDetails.Select(od => new OrderDetailDTO {
                    Id = od.Id,
                    Product = mc.Map<Product, ProductDTO>(od.Product),
                    SubPrice = od.UnitPrice * od.Count
                }).ToList(),
                Price = o.Price
            });

            var mockContext = new Mock<TestEntities>();
            var observableOrders = new ObservableCollection<Order>(_orders);
            mockContext.Setup(p => p.Orders).Returns(GetMockSet(observableOrders).Object);

            var query = mockContext.Object.Orders;
            var dtoQuery = config.ProjectTo<OrderDTO>(query, new IncludePath[] { });
            var dtoList = dtoQuery.ToList();

            Assert.IsNull(dtoList[3].OrderDetails);
        }

        [Test]
        public void Project_Orders_With_Details_Product_Custom_Expression() {
            var config = new MapConfiguration();
            config.RegisterMap<Order, OrderDTO>((o, mc) => new OrderDTO {
                Id = o.Id,
                OrderNo = o.OrderNo,
                OrderDetails = o.OrderDetails.Select(od => new OrderDetailDTO {
                    Id = od.Id,
                    Product = mc.Map<Product, ProductDTO>(od.Product),
                    SubPrice = od.UnitPrice * od.Count
                }).ToList(),
                Price = o.Price
            });
            config.RegisterMap<Product, ProductDTO>();
            config.RegisterMap<Company, CompanyDTO>();

            var mockContext = new Mock<TestEntities>();
            var observableOrders = new ObservableCollection<Order>(_orders);
            mockContext.Setup(p => p.Orders).Returns(GetMockSet(observableOrders).Object);

            var query = mockContext.Object.Orders;
            var dtoQuery = config.ProjectTo<Order, OrderDTO>(query, o => o.OrderDetails.Select(od => od.Product).Select(p => p.Supplier));
            var dtoList = dtoQuery.ToList();

            Assert.AreEqual(
                dtoList[3].OrderDetails.ToList()[2].Product.Supplier.CompanyName,
                _orders[3].OrderDetails[2].Product.Supplier.CompanyName
            );
        }

        public static Mock<DbSet<T>> GetMockSet<T>(ObservableCollection<T> list) where T : class {
            var queryable = list.AsQueryable();
            var mockList = new Mock<DbSet<T>>(MockBehavior.Loose);

            mockList.As<IQueryable<T>>().Setup(m => m.Provider).Returns(queryable.Provider);
            mockList.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
            mockList.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
            mockList.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
            mockList.Setup(m => m.Include(It.IsAny<string>())).Returns(mockList.Object);
            mockList.Setup(m => m.Local).Returns(list);
            mockList.Setup(m => m.Add(It.IsAny<T>())).Returns((T a) => {
                list.Add(a); return a;
            });
            mockList.Setup(m => m.AddRange(It.IsAny<IEnumerable<T>>())).Returns((IEnumerable<T> a) => {
                foreach (var item in a.ToArray()) list.Add(item); return a;
            });
            mockList.Setup(m => m.Remove(It.IsAny<T>())).Returns((T a) => {
                list.Remove(a); return a;
            });
            mockList.Setup(m => m.RemoveRange(It.IsAny<IEnumerable<T>>())).Returns((IEnumerable<T> a) => {
                foreach (var item in a.ToArray()) list.Remove(item); return a;
            });

            return mockList;
        }
    }
}