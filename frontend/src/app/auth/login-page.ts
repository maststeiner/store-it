import { Component, inject } from '@angular/core';

import { AuthService } from '../core/auth.service';
import { TranslatePipe } from '../core/translate';

@Component({
  selector: 'app-login-page',
  imports: [TranslatePipe],
  templateUrl: './login-page.html',
})
export class LoginPage {
  protected readonly auth = inject(AuthService);
}
