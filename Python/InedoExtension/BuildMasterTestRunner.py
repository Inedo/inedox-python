from unittest import TextTestResult, TextTestRunner, main
from json import dumps
from time import time
from timeit import default_timer as perf_counter
from traceback import format_exception

from unittest.result import TestResult

class BuildMasterTestResult(TextTestResult):
	def __init__(self, *args, **kwargs):
		TextTestResult.__init__(self, *args, **kwargs)

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
		TextTestResult.startTestRun(self)
		self._bmMessage({
			'Type': 'StartSuite'
		})

	def stopTestRun(self):
		"""Called once after all tests are executed."""
		TextTestResult.stopTestRun(self)
		self._bmMessage({
			'Type': 'StopSuite'
		})

	def startTest(self, test):
		"""Called when the given test is about to be run"""
		TextTestResult.startTest(self, test)
		self._bmMessage({
			'Type': 'StartCase'
		}, test)

	def stopTest(self, test):
		"""Called when the given test has been run"""
		message = {
			'Type': 'StopCase',
			'Output': self._stdout_buffer.getvalue() if self.buffer and self._stdout_buffer is not None else '',
			'Error': self._stderr_buffer.getvalue() if self.buffer and self._stderr_buffer is not None else ''
		}
		TextTestResult.stopTest(self, test)
		self._bmMessage(message, test)

	def addError(self, test, err):
		"""Called when an error has occurred. 'err' is a tuple of values as returned by sys.exc_info()."""
		TextTestResult.addError(self, test, err)
		self._bmMessage({
			'Type': 'Error',
			'Err': format_exception(*err)
		}, test)

	def addFailure(self, test, err):
		"""Called when an error has occurred. 'err' is a tuple of values as returned by sys.exc_info()."""
		TextTestResult.addFailure(self, test, err)
		self._bmMessage({
			'Type': 'Failure',
			'Err': format_exception(*err)
		}, test)

	def addSubTest(self, test, subtest, err):
		"""Called at the end of a subtest. 
		'err' is None if the subtest ended successfully, otherwise it's a
		tuple of values as returned by sys.exc_info().
		"""
		TextTestResult.addSubTest(self, test, subtest, err)
		pass

	def addSuccess(self, test):
		"""Called when a test has completed successfully"""
		TextTestResult.addSuccess(self, test)
		self._bmMessage({
			'Type': 'Success'
		}, test)

	def addSkip(self, test, reason):
		"""Called when a test is skipped."""
		TextTestResult.addSkip(self, test, reason)
		self._bmMessage({
			'Type': 'Skip',
			'Message': reason
		}, test)

	def addExpectedFailure(self, test, err):
		"""Called when an expected failure/error occurred."""
		TextTestResult.addExpectedFailure(self, test, err)
		self._bmMessage({
			'Type': 'ExpectedFailure',
			'Err': format_exception(*err)
		}, test)

	def addUnexpectedSuccess(self, test):
		"""Called when a test was expected to fail, but succeed."""
		TextTestResult.addUnexpectedSuccess(self, test)
		self._bmMessage({
			'Type': 'UnexpectedSuccess'
		}, test)

class BuildMasterTestRunner(TextTestRunner):
	def __init__(self, **kwargs):
		TextTestRunner.__init__(self, resultclass = BuildMasterTestResult, **kwargs)

if __name__ == '__main__':
	main(module = None, testRunner = BuildMasterTestRunner)
