import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AuthService } from './core/auth.service';
import { LanguageService } from './core/language.service';
import { TranslatePipe } from './core/translate';
import { SessionMenu } from './shared/session-menu';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, TranslatePipe, SessionMenu],
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly language = inject(LanguageService);
  protected readonly auth = inject(AuthService);

  /**
   * Startup work belongs in the lifecycle hook, not the constructor: the constructor is for
   * dependency injection, and an async call started there escapes Angular's error handling
   * (Sonar S7059). ngOnInit still runs before the template is rendered, so the language is
   * resolved by first paint.
   */
  ngOnInit(): void {
    this.language.init();
    void this.auth.initCsrf();
  }
}
