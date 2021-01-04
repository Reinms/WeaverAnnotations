

namespace TestInlineIL
{
    using NUnit.Framework;
    using Test.InlineIL;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    public class Tests
    {
        [DatapointSource]
        public IEnumerable<int> ints => Enumerable.Range(-10, 10);

        [Theory]
        public void Test1(int a, int b)
        {
            Assert.That(ILArith.Add(a, b) == a + b);
            Assert.That(ILArith.And(a, b) == (a & b));
            Assert.That(ILArith.Sub(a, b) == a - b);
            Assert.That(ILArith.Div(a, b) == a / b);
            Assert.That(ILArith.Or(a, b) == (a | b));
            Assert.That(ILArith.Mul(a, b) == a * b);
            Assert.That(ILArith.Xor(a, b) == (a ^ b));
        }
    }
}