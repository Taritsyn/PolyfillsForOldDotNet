using System;

using NUnit.Framework;

namespace PolyfillsForOldDotNet.System.Buffers.Tests
{
	[TestFixture]
	public class ArrayPoolUnitTests
	{
		private const int MaxEventWaitTimeoutInMs = 200;

		private struct TestStruct
		{
			internal string InternalRef;
		}


		/*
			NOTE - due to test parallelism and sharing, use an instance pool for testing unless necessary
		*/
		[Test]
		public static void SharedInstanceCreatesAnInstanceOnFirstCall()
		{
			Assert.IsNotNull(ArrayPool<byte>.Shared);
		}

		[Test]
		public static void SharedInstanceOnlyCreatesOneInstanceOfOneType()
		{
			ArrayPool<byte> instance = ArrayPool<byte>.Shared;

			Assert.AreSame(instance, ArrayPool<byte>.Shared);
		}

		[Test]
		public static void CreateWillCreateMultipleInstancesOfTheSameType()
		{
			Assert.AreNotSame(ArrayPool<byte>.Create(), ArrayPool<byte>.Create());
		}

		[Theory]
		[TestCase(0)]
		[TestCase(-1)]
		public static void CreatingAPoolWithInvalidArrayCountThrows(int length)
		{
			Assert.Throws<ArgumentOutOfRangeException>(
				() => ArrayPool<byte>.Create(maxArraysPerBucket: length, maxArrayLength: 16));
		}

		[Theory]
		[TestCase(0)]
		[TestCase(-1)]
		public static void CreatingAPoolWithInvalidMaximumArraySizeThrows(int length)
		{
			Assert.Throws<ArgumentOutOfRangeException>(
				() => ArrayPool<byte>.Create(maxArrayLength: length, maxArraysPerBucket: 1));
		}

		[Theory]
		[TestCase(1)]
		[TestCase(16)]
		[TestCase(0x40000000)]
		[TestCase(0x7FFFFFFF)]
		public static void CreatingAPoolWithValidMaximumArraySizeSucceeds(int length)
		{
			var pool = ArrayPool<byte>.Create(maxArrayLength: length, maxArraysPerBucket: 1);

			Assert.IsNotNull(pool);
			Assert.IsNotNull(pool.Rent(1));
		}

		[Theory]
		[TestCase(-1)]
		public static void RentingWithInvalidLengthThrows(int length)
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create();

			Assert.Throws<ArgumentOutOfRangeException>(() => pool.Rent(length));
		}

		[Test]
		public static void RentingGiganticArraySucceedsOrOOMs()
		{
			try
			{
				int len = 0x70000000;
				byte[] buffer = ArrayPool<byte>.Shared.Rent(len);

				Assert.IsNotNull(buffer);
				Assert.IsTrue(buffer.Length >= len);
			}
			catch (OutOfMemoryException)
			{ }
		}

		[Test]
		public static void Renting0LengthArrayReturnsSingleton()
		{
			byte[] zero0 = ArrayPool<byte>.Shared.Rent(0);
			byte[] zero1 = ArrayPool<byte>.Shared.Rent(0);
			byte[] zero2 = ArrayPool<byte>.Shared.Rent(0);
			byte[] one = ArrayPool<byte>.Shared.Rent(1);

			Assert.AreSame(zero0, zero1);
			Assert.AreSame(zero1, zero2);
			Assert.AreNotSame(zero2, one);

			ArrayPool<byte>.Shared.Return(zero0);
			ArrayPool<byte>.Shared.Return(zero1);
			ArrayPool<byte>.Shared.Return(zero2);
			ArrayPool<byte>.Shared.Return(one);

			Assert.AreSame(zero0, ArrayPool<byte>.Shared.Rent(0));
		}

		[Test]
		public static void RentingMultipleArraysGivesBackDifferentInstances()
		{
			ArrayPool<byte> instance = ArrayPool<byte>.Create(maxArraysPerBucket: 2, maxArrayLength: 16);

			Assert.AreNotSame(instance.Rent(100), instance.Rent(100));
		}

		[Test]
		public static void RentingMoreArraysThanSpecifiedInCreateWillStillSucceed()
		{
			ArrayPool<byte> instance = ArrayPool<byte>.Create(maxArraysPerBucket: 1, maxArrayLength: 16);

			Assert.IsNotNull(instance.Rent(100));
			Assert.IsNotNull(instance.Rent(100));
		}

		[Test]
		public static void RentCanReturnBiggerArraySizeThanRequested()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArraysPerBucket: 1, maxArrayLength: 32);
			byte[] rented = pool.Rent(27);

			Assert.IsNotNull(rented);
			Assert.AreEqual(rented.Length, 32);
		}

		[Test]
		public static void RentingAnArrayWithLengthGreaterThanSpecifiedInCreateStillSucceeds()
		{
			Assert.IsNotNull(ArrayPool<byte>.Create(maxArrayLength: 100, maxArraysPerBucket: 1).Rent(200));
		}

		[Test]
		public static void CallingReturnBufferWithNullBufferThrows()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create();

			Assert.Throws<ArgumentNullException>(() => pool.Return(null));
		}

		private static void FillArray(byte[] buffer)
		{
			for (byte i = 0; i < buffer.Length; i++)
			{
				buffer[i] = i;
			}
		}

		private static void CheckFilledArray(byte[] buffer, Action<byte, byte> assert)
		{
			for (byte i = 0; i < buffer.Length; i++)
			{
				assert(buffer[i], i);
			}
		}

		[Test]
		public static void CallingReturnWithoutClearingDoesNotClearTheBuffer()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create();
			byte[] buffer = pool.Rent(4);

			FillArray(buffer);
			pool.Return(buffer, clearArray: false);

			CheckFilledArray(buffer, (byte b1, byte b2) => Assert.AreEqual(b1, b2));
		}

		[Test]
		public static void CallingReturnWithClearingDoesClearTheBuffer()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create();
			byte[] buffer = pool.Rent(4);

			FillArray(buffer);
			// Note - yes this is bad to hold on to the old instance but we need to validate the contract
			pool.Return(buffer, clearArray: true);

			CheckFilledArray(buffer, (byte b1, byte b2) => Assert.AreEqual(b1, default(byte)));
		}

		[Test]
		public static void CallingReturnOnReferenceTypeArrayDoesNotClearTheArray()
		{
			ArrayPool<string> pool = ArrayPool<string>.Create();
			string[] array = pool.Rent(2);
			array[0] = "foo";
			array[1] = "bar";

			pool.Return(array, clearArray: false);

			Assert.IsNotNull(array[0]);
			Assert.IsNotNull(array[1]);
		}

		[Test]
		public static void CallingReturnOnReferenceTypeArrayAndClearingSetsTypesToNull()
		{
			ArrayPool<string> pool = ArrayPool<string>.Create();
			string[] array = pool.Rent(2);
			array[0] = "foo";
			array[1] = "bar";

			pool.Return(array, clearArray: true);

			Assert.IsNull(array[0]);
			Assert.IsNull(array[1]);
		}

		[Test]
		public static void CallingReturnOnValueTypeWithInternalReferenceTypesAndClearingSetsValueTypeToDefault()
		{
			ArrayPool<TestStruct> pool = ArrayPool<TestStruct>.Create();
			TestStruct[] array = pool.Rent(2);
			array[0].InternalRef = "foo";
			array[1].InternalRef = "bar";

			pool.Return(array, clearArray: true);

			Assert.AreEqual(array[0], default(TestStruct));
			Assert.AreEqual(array[1], default(TestStruct));
		}

		[Test]
		public static void TakingAllBuffersFromABucketPlusAnAllocatedOneShouldAllowReturningAllBuffers()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 16, maxArraysPerBucket: 1);
			byte[] rented = pool.Rent(16);
			byte[] allocated = pool.Rent(16);

			pool.Return(rented);
			pool.Return(allocated);
		}

		[Test]
		public static void NewDefaultArrayPoolWithSmallBufferSizeRoundsToOurSmallestSupportedSize()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 8, maxArraysPerBucket: 1);
			byte[] rented = pool.Rent(8);

			Assert.IsTrue(rented.Length == 16);
		}

		[Test]
		public static void ReturningABufferGreaterThanMaxSizeDoesNotThrow()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 16, maxArraysPerBucket: 1);
			byte[] rented = pool.Rent(32);

			pool.Return(rented);
		}

		[Test]
		public static void RentingAllBuffersAndCallingRentAgainWillAllocateBufferAndReturnIt()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 16, maxArraysPerBucket: 1);
			byte[] rented1 = pool.Rent(16);
			byte[] rented2 = pool.Rent(16);

			Assert.IsNotNull(rented1);
			Assert.IsNotNull(rented2);
		}

		[Test]
		public static void RentingReturningThenRentingABufferShouldNotAllocate()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 16, maxArraysPerBucket: 1);
			byte[] bt = pool.Rent(16);
			int id = bt.GetHashCode();

			pool.Return(bt);
			bt = pool.Rent(16);

			Assert.AreEqual(id, bt.GetHashCode());
		}

		[Test]
		public static void CanRentManySizedBuffers()
		{
			var pool = ArrayPool<byte>.Create();

			for (int i = 1; i < 10000; i++)
			{
				byte[] buffer = pool.Rent(i);
				Assert.AreEqual(i <= 16 ? 16 : RoundUpToPowerOf2(i), buffer.Length);
				pool.Return(buffer);
			}
		}

		private static int RoundUpToPowerOf2(int i)
		{
			// http://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
			--i;
			i |= i >> 1;
			i |= i >> 2;
			i |= i >> 4;
			i |= i >> 8;
			i |= i >> 16;
			return i + 1;
		}

		[Theory]
		[TestCase(1, 16)]
		[TestCase(15, 16)]
		[TestCase(16, 16)]
		[TestCase(1023, 1024)]
		[TestCase(1024, 1024)]
		[TestCase(4096, 4096)]
		[TestCase(1024 * 1024, 1024 * 1024)]
		[TestCase(1024 * 1024 + 1, 1024 * 1024 + 1)]
		[TestCase(1024 * 1024 * 2, 1024 * 1024 * 2)]
		public static void RentingSpecificLengthsYieldsExpectedLengths(int requestedMinimum, int expectedLength)
		{
			byte[] buffer = ArrayPool<byte>.Create().Rent(requestedMinimum);

			Assert.IsNotNull(buffer);
			Assert.AreEqual(expectedLength, buffer.Length);
		}

		[Test]
		public static void RentingAfterPoolExhaustionReturnsSizeForCorrespondingBucket_SmallerThanLimit()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 64, maxArraysPerBucket: 2);

			Assert.AreEqual(16, pool.Rent(15).Length); // try initial bucket
			Assert.AreEqual(16, pool.Rent(15).Length);

			Assert.AreEqual(32, pool.Rent(15).Length); // try one more level
			Assert.AreEqual(32, pool.Rent(15).Length);

			Assert.AreEqual(16, pool.Rent(15).Length); // fall back to original size
		}

		[Test]
		public static void RentingAfterPoolExhaustionReturnsSizeForCorrespondingBucket_JustBelowLimit()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 64, maxArraysPerBucket: 2);

			Assert.AreEqual(32, pool.Rent(31).Length); // try initial bucket
			Assert.AreEqual(32, pool.Rent(31).Length);

			Assert.AreEqual(64, pool.Rent(31).Length); // try one more level
			Assert.AreEqual(64, pool.Rent(31).Length);

			Assert.AreEqual(32, pool.Rent(31).Length); // fall back to original size
		}

		[Test]
		public static void RentingAfterPoolExhaustionReturnsSizeForCorrespondingBucket_AtLimit()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 64, maxArraysPerBucket: 2);

			Assert.AreEqual(64, pool.Rent(63).Length); // try initial bucket
			Assert.AreEqual(64, pool.Rent(63).Length);

			Assert.AreEqual(64, pool.Rent(63).Length); // still get original size
		}

		[Test]
		public static void ReturningANonPooledBufferOfDifferentSizeToThePoolThrows()
		{
			ArrayPool<byte> pool = ArrayPool<byte>.Create(maxArrayLength: 16, maxArraysPerBucket: 1);

			byte[] buffer = pool.Rent(15);
			Assert.Throws<ArgumentException>(() => pool.Return(new byte[1]));

			buffer = pool.Rent(15);
			Assert.AreEqual(buffer.Length, 16);
		}
	}
}