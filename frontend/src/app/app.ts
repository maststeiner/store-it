import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { LanguageService } from './core/language.service';
import { TranslatePipe } from './core/translate';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule, TranslatePipe],
  templateUrl: './app.html',
})
export class App {
  protected readonly language = inject(LanguageService);

  constructor() {
    this.language.init();
  }
}
