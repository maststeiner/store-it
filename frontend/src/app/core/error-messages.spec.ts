import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { ErrorMessages } from './error-messages';
import { ApiError } from './storage-api';
import { TranslateService } from './translate';

describe('ErrorMessages', () => {
  let messages: ErrorMessages;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', {
      errors: {
        generic: 'Something went wrong.',
        storage: { name: { empty: 'Please enter a storage name.' } },
      },
    });
    messages = TestBed.inject(ErrorMessages);
  });

  it('translates a known errorCode', () => {
    const message = messages.messageFor(new ApiError(400, 'storage.name.empty'));
    expect(message).toBe('Please enter a storage name.');
  });

  it('falls back to the generic message for unknown codes', () => {
    const message = messages.messageFor(new ApiError(400, 'something.unknown'));
    expect(message).toBe('Something went wrong.');
  });

  it('falls back to the generic message for non-API errors', () => {
    const message = messages.messageFor(new Error('boom'));
    expect(message).toBe('Something went wrong.');
  });
});
