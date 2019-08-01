from unittest import result, TextTestRunner
from json import dumps
from time import time, perf_counter
from traceback import format_exception

class BuildMasterTestResult(result.TestResult):
	def __init__(self, stream, descriptions, verbosity, failfast):
		super.__init__(stream, descriptions, verbosity)
		self.failfast = failfast
		self.buffer = True

	def _bmMessage(self, message, test = None):
		if test is not None:
			message['Test'] = {
				'ID': test.id(),
				'Desc': test.shortDescription()
			}
		message['Now'] = time()
		message['Time'] = perf_counter()
		self._original_stdout.write('__BuildMasterPythonTestRunner__{0}\n'.format(dumps(message)))

	def startTestRun(self):
		"""Called once before any tests are executed."""
		self._bmMessage({
			'Type': 'StartSuite'
		})

	def stopTestRun(self):
		"""Called once after all tests are executed."""
		self._bmMessage({
			'Type': 'StopSuite'
		})

	def startTest(self, test):
		"""Called when the given test is about to be run"""
		super().startTest(test)
		self._bmMessage({
			'Type': 'StartCase'
		}, test)

	def stopTest(self, test):
		"""Called when the given test has been run"""
		message = {
			'Type': 'StopCase',
			'Output': sys.stdout.getvalue(),
			'Error': sys.stderr.getvalue()
		}
		super().stopTest(test)
		self._bmMessage(message, test)

	def addError(self, test, err):
		"""Called when an error has occurred. 'err' is a tuple of values as returned by sys.exc_info()."""
		super().addError(test, err)
		self._bmMessage({
			'Type': 'Error',
			'Err': format_exception(*err)
		}, test)

	def addFailure(self, test, err):
		"""Called when an error has occurred. 'err' is a tuple of values as returned by sys.exc_info()."""
		super().addFailure(test, err)
		self._bmMessage({
			'Type': 'Failure',
			'Err': format_exception(*err)
		}, test)

	def addSubTest(self, test, subtest, err):
		"""Called at the end of a subtest. 
		'err' is None if the subtest ended successfully, otherwise it's a
		tuple of values as returned by sys.exc_info().
		"""
		super().addSubTest(test, subtest, err)
		pass

	def addSuccess(self, test):
		"""Called when a test has completed successfully"""
		self._bmMessage({
			'Type': 'Success'
		}, test)

	def addSkip(self, test, reason):
		"""Called when a test is skipped."""
		super().addSkip(test, reason)
		self._bmMessage({
			'Type': 'Skip',
			'Message': reason
		}, test)

	def addExpectedFailure(self, test, err):
		"""Called when an expected failure/error occurred."""
		super().addExpectedFailure(test, err)
		self._bmMessage({
			'Type': 'ExpectedFailure',
			'Err': format_exception(*err)
		}, test)

	def addUnexpectedSuccess(self, test):
		"""Called when a test was expected to fail, but succeed."""
		super().addUnexpectedSuccess(test)
		self._bmMessage({
			'Type': 'UnexpectedSuccess'
		}, test)

class BuildMasterTestRunner(TextTestRunner):
	def __init__(self, **kwargs):
		super(BuildMasterTestRunner, self).__init__(self, resultclass = BuildMasterTestResult, buffer = True, **kwargs)
