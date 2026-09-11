const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
let BufferProcessor;
vm.runInNewContext(fs.readFileSync('.local/voice-input-worklet.js', 'utf8'), {
  AudioWorkletProcessor: class { constructor() { this.port = {postMessage: value => this.error = value.error}; } },
  Float32Array, sampleRate: 4,
  registerProcessor: (_, implementation) => BufferProcessor = implementation,
});
const buffer = new BufferProcessor();
const message = data => buffer.port.onmessage({data});
const process = values => {
  const output = new Float32Array(2);
  buffer.process([[new Float32Array(values)]], [[output]]);
  return Array.from(output);
};
assert.deepEqual(process([9, 9]), [0, 0]); // Never retain unheld microphone samples.
message({type:'held',value:true});
assert.deepEqual(process([1, 2]), [0, 0]);
assert.deepEqual(process([3, 4]), [0, 0]);
message({type:'held',value:false});
assert.deepEqual(process([9, 9]), [0, 0]);
message({type:'ready'});
assert.deepEqual(process([9, 9]), [1, 2]); // Release during startup preserves first words.
assert.deepEqual(process([9, 9]), [3, 4]);
assert.deepEqual(process([9, 9]), [0, 0]);
message({type:'held',value:true});
assert.deepEqual(process([5, 6]), [5, 6]); // Warm input has no queue delay.
message({type:'held',value:false});
assert.deepEqual(process([9, 9]), [0, 0]);
const overflow = new BufferProcessor();
overflow.port.onmessage({data:{type:'held',value:true}});
for (let i = 0; i < 61; i++) overflow.process([[new Float32Array([1,2])]], [[new Float32Array(2)]]);
assert.ok(overflow.error);
assert.equal(overflow.samples, 0);
assert.equal(overflow.held, false);
console.log('PASS: held-only audio, ordered cold-start replay, release-before-ready, immediate warm input and bounded overflow');
