﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Castle.Core.Interceptor;

namespace Moq
{
	/// <summary>
	/// Implements the actual interception and method invocation for 
	/// all mocks, even MBROs (via the <see cref="RemotingProxy"/>.
	/// </summary>
	internal class Interceptor : MarshalByRefObject, IInterceptor
	{
		MockBehavior behavior;
		Dictionary<string, IProxyCall> calls = new Dictionary<string, IProxyCall>();

		public Interceptor(MockBehavior behavior)
		{
			this.behavior = behavior;
		}

		internal void Verify()
		{
			VerifyOrThrow(call => call.IsVerifiable && !call.Invoked);
		}

		internal void VerifyAll()
		{
			VerifyOrThrow(call => !call.Invoked);
		}

		private void VerifyOrThrow(Predicate<IProxyCall> match)
		{
			var failures = new List<IProxyCall>();
			foreach (var call in calls.Values)
			{
				if (match(call))
					failures.Add(call);
			}

			if (failures.Count > 0)
			{
				throw new MockException(MockException.ExceptionReason.VerificationFailed,
					String.Format(Properties.Resources.VerficationFailed, 
						String.Join("\r\n", 
						failures.ConvertAll(call => call.ExpectExpression.ToString()).ToArray())
					));
			}
		}

		public void AddCall(IProxyCall call)
		{
			calls[call.ExpectExpression.ToString()] = call;
		}

		public void Intercept(IInvocation invocation)
		{
			var call = (from c in calls.Values
					   where c.Matches(invocation)
					   select c).FirstOrDefault();

			if (call == null)
			{
				if (behavior == MockBehavior.Strict)
				{
					throw new MockException(
						MockException.ExceptionReason.NoExpectation,
						behavior,
						invocation);
				}
				else if (behavior == MockBehavior.Normal)
				{
					if (invocation.Method.DeclaringType.IsInterface)
					{
						throw new MockException(
							MockException.ExceptionReason.InterfaceNoExpectation,
							behavior, invocation);
					}
					else if (invocation.Method.IsAbstract)
					{
						throw new MockException(
							MockException.ExceptionReason.AbstractNoExpectation, 
							behavior, invocation);
					}
				}
			}

			if (call != null)
			{
				call.Execute(invocation);
			}
			else if (invocation.Method.DeclaringType == typeof(object))
			{
				invocation.Proceed();
			}
			else if (invocation.TargetType.IsClass &&
				!invocation.Method.IsAbstract)
			{
				// For mocked classes, if the target method was not abstract, 
				// invoke directly.
				// Will only get here for Normal and below behaviors.
				invocation.Proceed();
			}
			else if (invocation.Method != null && invocation.Method.ReturnType != null &&
				invocation.Method.ReturnType != typeof(void))
			{
				if (behavior == MockBehavior.Loose)
				{
					// Return default value.
					if (invocation.Method.ReturnType.IsValueType)
					    invocation.ReturnValue = 0;
					else
					    invocation.ReturnValue = null;
				}
				else
				{
					// Will only get this far for Relaxed.
					throw new MockException(
						MockException.ExceptionReason.ReturnValueNoExpectation,
						behavior, invocation);
				}
			}
		}
	}
}
